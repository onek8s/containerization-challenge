using Humongous.Quoting.Api.Hubs;
using System.Security.Claims;
using Humongous.Quoting.Api.Infrastructure;
using Humongous.Quoting.Contracts;
using Humongous.Quoting.Contracts.Hubs;
using Humongous.Quoting.Contracts.Messages;
using Humongous.Quoting.Data;
using Humongous.Quoting.Data.Entities;
using Humongous.Quoting.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Humongous.Quoting.Api.Services;

public sealed class QuoteService(
    QuotingDbContext db,
    NumberGenerator numbers,
    RatingQueue queue,
    HubNotifier notifier,
    IPublishEndpoint bus,
    TimeProvider clock,
    ILogger<QuoteService> logger)
{
    private static readonly RiskValidator Validator = new();

    private DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    public async Task<IResult> CreateAsync(QuoteRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var agencyCode = user.AgencyCode();
        if (agencyCode is null)
            return Results.Forbid();

        var check = await CheckRequestAsync(request.Risk, request.InceptionDate, ct);
        if (check is not null) return check;

        var agency = await db.Agencies.FindAsync([agencyCode], ct);
        if (agency is not { Active: true })
            return ProblemResults.Validation("agency", "Your agency is not set up to quote. Contact Humongous broker services.");

        var product = ProductCatalogue.ForDate(request.InceptionDate);
        var commission = request.CommissionRate ?? agency.CommissionRate;
        if (commission < 0 || commission > product.MaxCommission)
            return ProblemResults.Validation("commissionRate", $"Commission must be between 0 and {product.MaxCommission:P0}.");

        var now = clock.GetUtcNow().UtcDateTime;
        var sequence = await numbers.NextAsync(NumberGenerator.QuoteSequence, ct);
        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            Reference = References.Quote(now.Year, sequence),
            AgencyCode = agency.Code,
            BrokerReference = request.BrokerReference,
            CreatedBy = user.DisplayName(),
            Status = QuoteStatus.Draft,
            ProductVersion = product.Version,
            InceptionDate = request.InceptionDate,
            CommissionRate = commission,
            CreatedUtc = now,
            UpdatedUtc = now,
            ConcurrencyStamp = Guid.NewGuid()
        };
        quote.SetRisk(request.Risk with { Insured = request.Risk.Insured with { Postcode = Postcode.Normalise(request.Risk.Insured.Postcode) } });
        quote.Touch("Created", user.DisplayName());
        quote.Move(QuoteStatus.Rating, user.DisplayName());
        db.Quotes.Add(quote);
        await db.SaveChangesAsync(ct);

        await queue.EnqueueAsync(quote.Id, ct);
        logger.LogInformation("Quote {Reference} created by {User} for {Agency}", quote.Reference, user.DisplayName(), agency.Code);
        return Results.Accepted($"/api/quotes/{quote.Id}", new QuoteCreated(quote.Id, quote.Reference, quote.Status));
    }

    public async Task<IResult> RequoteAsync(Guid id, RequoteRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var quote = await LoadAsync(id, ct);
        if (quote is null || !user.CanSee(quote.AgencyCode)) return Results.NotFound();
        if (!QuoteLifecycle.CanMove(quote.Status, QuoteStatus.Rating))
            return ProblemResults.Conflict($"A quote in status {quote.Status} cannot be changed.");

        var inception = request.InceptionDate ?? quote.InceptionDate;
        var check = await CheckRequestAsync(request.Risk, inception, ct);
        if (check is not null) return check;

        var product = ProductCatalogue.ForDate(inception);
        var commission = request.CommissionRate ?? quote.CommissionRate;
        if (commission < 0 || commission > product.MaxCommission)
            return ProblemResults.Validation("commissionRate", $"Commission must be between 0 and {product.MaxCommission:P0}.");

        quote.SetRisk(request.Risk with { Insured = request.Risk.Insured with { Postcode = Postcode.Normalise(request.Risk.Insured.Postcode) } });
        quote.InceptionDate = inception;
        quote.ProductVersion = product.Version;
        quote.CommissionRate = commission;
        quote.UnderwriterNote = null;
        quote.DecidedBy = null;
        quote.DecidedUtc = null;
        quote.Move(QuoteStatus.Rating, user.DisplayName(), "Requote");
        await db.SaveChangesAsync(ct);
        await queue.EnqueueAsync(quote.Id, ct);
        await notifier.QuoteChanged(new QuoteNotice(quote.Id, quote.Reference, quote.AgencyCode, quote.Status, null, null));
        return Results.Accepted($"/api/quotes/{quote.Id}", new QuoteCreated(quote.Id, quote.Reference, quote.Status));
    }

    public async Task<IResult> RequestBindAsync(Guid id, BindRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var quote = await LoadAsync(id, ct);
        if (quote is null || !user.CanSee(quote.AgencyCode)) return Results.NotFound();
        if (!QuoteLifecycle.IsBindable(quote.Status))
            return ProblemResults.Conflict($"A quote in status {quote.Status} cannot be bound.");
        if (quote.ValidUntil is { } validUntil && validUntil < Today)
            return ProblemResults.Conflict("The quote has expired. Requote to get current terms.");
        if (quote.InceptionDate < Today)
            return ProblemResults.Validation("inceptionDate", "The inception date has passed. Requote with a new inception date.");

        quote.Move(QuoteStatus.BindRequested, user.DisplayName());
        await db.SaveChangesAsync(ct);
        await bus.Publish(new BindRequested
        {
            QuoteId = quote.Id,
            RequestedBy = user.DisplayName(),
            InsuredEmail = request.InsuredEmail,
            SendDocumentsToInsured = request.SendDocumentsToInsured
        }, ct);
        await notifier.QuoteChanged(new QuoteNotice(quote.Id, quote.Reference, quote.AgencyCode, quote.Status, quote.Total, null));
        logger.LogInformation("Bind requested for {Reference} by {User}", quote.Reference, user.DisplayName());
        return Results.Accepted($"/api/quotes/{quote.Id}", new QuoteCreated(quote.Id, quote.Reference, quote.Status));
    }

    public async Task<IResult> NotTakenUpAsync(Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        var quote = await LoadAsync(id, ct);
        if (quote is null || !user.CanSee(quote.AgencyCode)) return Results.NotFound();
        if (!QuoteLifecycle.CanMove(quote.Status, QuoteStatus.NotTakenUp))
            return ProblemResults.Conflict($"A quote in status {quote.Status} cannot be closed.");
        quote.Move(QuoteStatus.NotTakenUp, user.DisplayName());
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    public async Task<IResult> DecideAsync(Guid id, ReferralDecisionRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var quote = await LoadAsync(id, ct);
        if (quote is null) return Results.NotFound();
        if (quote.Status != QuoteStatus.Referred)
            return ProblemResults.Conflict($"Quote {quote.Reference} is not referred (status {quote.Status}).");

        // Sums insured over the binding authority limit need a senior underwriter.
        if (request.Decision == ReferralDecision.Accept
            && quote.Referrals.Any(r => r.Code == "S01")
            && !user.IsInRole("SeniorUnderwriter"))
        {
            return Results.Problem("Only a senior underwriter can accept sums insured over the authority limit.", statusCode: StatusCodes.Status403Forbidden);
        }

        quote.DecidedBy = user.DisplayName();
        quote.DecidedUtc = clock.GetUtcNow().UtcDateTime;
        quote.UnderwriterNote = request.Note;

        if (request.Decision == ReferralDecision.Decline)
        {
            quote.Move(QuoteStatus.Declined, user.DisplayName(), request.Note);
        }
        else
        {
            var loadings = new Dictionary<SectionCode, decimal>();
            foreach (var (code, percent) in request.Loadings)
            {
                if (!SectionCodes.TryParse(code, out var section) || quote.Premium?.Sections.All(s => s.Section != section) != false)
                    return ProblemResults.Validation("loadings", $"Section {code} is not on the quote.");
                if (percent < -25m || percent > 250m)
                    return ProblemResults.Validation("loadings", "Loadings must be between -25% and +250%.");
                loadings[section] = percent;
            }

            var product = ProductCatalogue.Find(quote.ProductVersion)!;
            var technical = new RatingResult
            {
                Sections = quote.Premium!.Sections.Select(s => new SectionRating(s.Section, s.Technical, 0m, "")).ToList()
            };
            var premium = PremiumCalculator.Calculate(technical, product, quote.CommissionRate, loadings);
            quote.Premium = premium;
            quote.GrossPremium = premium.GrossPremium;
            quote.Total = premium.Total;
            quote.Loadings = loadings.ToDictionary(l => l.Key.ToCode(), l => l.Value);
            quote.ValidUntil = Today.AddDays(QuoteLifecycle.ValidityDays);
            quote.Move(QuoteStatus.Accepted, user.DisplayName(), request.Note);
        }

        await db.SaveChangesAsync(ct);
        await notifier.QuoteChanged(new QuoteNotice(quote.Id, quote.Reference, quote.AgencyCode, quote.Status, quote.Total, request.Note));
        await bus.Publish(new QuoteStatusChanged { QuoteId = quote.Id, Reference = quote.Reference, AgencyCode = quote.AgencyCode, Status = quote.Status, Total = quote.Total }, ct);
        if (quote.Status == QuoteStatus.Accepted)
            await bus.Publish(new DocumentRequested { DocumentType = DocumentType.QuoteSchedule, QuoteId = quote.Id, EmailToBroker = true }, ct);

        logger.LogInformation("Referral on {Reference} decided by {User}: {Decision}", quote.Reference, user.DisplayName(), request.Decision);
        return Results.Ok(quote.ToDetail());
    }

    public async Task<IResult> AssignAsync(Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        var quote = await LoadAsync(id, ct);
        if (quote is null) return Results.NotFound();
        quote.AssignedUnderwriter = user.DisplayName();
        quote.Touch("Assigned", user.DisplayName());
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private async Task<IResult?> CheckRequestAsync(RiskDetails risk, DateOnly inception, CancellationToken ct)
    {
        var validation = await Validator.ValidateAsync(risk, ct);
        if (!validation.IsValid) return ProblemResults.Validation(validation);
        if (InceptionValidator.Check(inception, Today) is { } error)
            return ProblemResults.Validation("inceptionDate", error);
        return null;
    }

    private Task<Quote?> LoadAsync(Guid id, CancellationToken ct) =>
        db.Quotes.Include(q => q.Referrals).Include(q => q.Events).Include(q => q.Agency)
            .AsSplitQuery()
            .FirstOrDefaultAsync(q => q.Id == id, ct);
}

using Humongous.Quoting.Contracts.Messages;
using Humongous.Quoting.Data;
using Humongous.Quoting.Data.Entities;
using Humongous.Quoting.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Humongous.PolicyService.Jobs;

/// <summary>
/// Invites renewal: 45 days before expiry each live policy is re-rated on the product version and
/// rate tables in force at the renewal date and a renewal quote is sent to the broker.
/// </summary>
public sealed class RenewalJob(
    QuotingDbContext db,
    NumberGenerator numbers,
    IRatingEngine engine,
    IPublishEndpoint bus,
    TimeProvider clock,
    IConfiguration configuration,
    ILogger<RenewalJob> logger) : IScheduledJob
{
    public string Name => "renewals";

    public async Task RunAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var horizon = today.AddDays(configuration.GetValue("Renewals:DaysBeforeExpiry", 45));
        var due = await db.Policies.Include(p => p.Agency)
            .Where(p => p.Status == PolicyStatus.Live && p.RenewalQuoteId == null && p.ExpiryDate >= today && p.ExpiryDate <= horizon)
            .OrderBy(p => p.ExpiryDate)
            .ToListAsync(ct);

        logger.LogInformation("{Count} policies due for renewal invitation up to {Horizon}", due.Count, horizon);
        foreach (var policy in due)
        {
            try
            {
                await InviteAsync(policy, today, ct);
            }
            catch (RatingException ex)
            {
                logger.LogError(ex, "Renewal of {PolicyNumber} could not be rated; it will be tried again on the next run", policy.PolicyNumber);
                db.ChangeTracker.Clear();
            }
        }
    }

    private async Task InviteAsync(Policy policy, DateOnly today, CancellationToken ct)
    {
        var inception = policy.ExpiryDate.AddDays(1);
        var product = ProductCatalogue.ForDate(inception);
        var agency = policy.Agency!;
        var commission = Math.Min(agency.CommissionRate, product.MaxCommission);
        var now = clock.GetUtcNow().UtcDateTime;

        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            Reference = References.Quote(now.Year, await numbers.NextAsync(NumberGenerator.QuoteSequence, ct)),
            AgencyCode = policy.AgencyCode,
            BrokerReference = policy.PolicyNumber,
            CreatedBy = "renewals",
            Status = QuoteStatus.Rating,
            ProductVersion = product.Version,
            InceptionDate = inception,
            CommissionRate = commission,
            RenewalOfPolicyId = policy.Id,
            CreatedUtc = now,
            UpdatedUtc = now,
            ConcurrencyStamp = Guid.NewGuid()
        };
        quote.SetRisk(policy.Risk);

        var rating = await engine.RateAsync(new RatingRequest(quote.Reference, product, inception, policy.Risk, RatingPurpose.Renewal), ct);
        var outcome = UnderwritingOutcome.Build(rating, product, policy.Risk, commission, today);

        quote.Premium = outcome.Premium;
        quote.TechnicalPremium = rating.TechnicalPremium;
        quote.GrossPremium = outcome.Premium.GrossPremium;
        quote.Total = outcome.Premium.Total;
        quote.Clauses = rating.Clauses.ToList();
        quote.RatingEngine = $"{engine.Name} {rating.EngineVersion}".Trim();
        quote.RatingTables = rating.TableVersion;
        quote.ValidUntil = inception;
        quote.Referrals.AddRange(outcome.Messages.Select(r => new QuoteReferral { QuoteId = quote.Id, Code = r.Code, Text = r.Text, Source = r.Source }));
        if (outcome.Status == QuoteStatus.Referred) quote.ReferredUtc = now;
        quote.Touch("Created", "renewals", $"Renewal of {policy.PolicyNumber}");
        quote.Move(outcome.Status, "renewals", $"{rating.TableVersion}; expiring gross {Money.Format(policy.AnnualGrossPremium)}");
        db.Quotes.Add(quote);

        policy.RenewalQuoteId = quote.Id;
        policy.RenewalInvitedUtc = now;
        policy.ConcurrencyStamp = Guid.NewGuid();
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Renewal quote {Reference} for {PolicyNumber}: {Status}, gross {Gross} (expiring {Expiring})",
            quote.Reference, policy.PolicyNumber, quote.Status, quote.GrossPremium, policy.AnnualGrossPremium);

        await bus.Publish(new RenewalInvited { PolicyId = policy.Id, RenewalQuoteId = quote.Id, AgencyCode = policy.AgencyCode, Status = quote.Status }, ct);
        if (quote.Status == QuoteStatus.Quoted)
            await bus.Publish(new DocumentRequested { DocumentType = DocumentType.RenewalInvitation, QuoteId = quote.Id, PolicyId = policy.Id }, ct);
    }
}

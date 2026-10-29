using System.Security.Claims;
using Humongous.Quoting.Api.Infrastructure;
using Humongous.Quoting.Api.Services;
using Humongous.Quoting.Contracts;
using Humongous.Quoting.Data;
using Humongous.Quoting.Domain;
using Microsoft.EntityFrameworkCore;

namespace Humongous.Quoting.Api.Endpoints;

public static class QuoteEndpoints
{
    public static RouteGroupBuilder MapQuotes(this RouteGroupBuilder api)
    {
        var quotes = api.MapGroup("/quotes").WithTags("Quotes");

        quotes.MapGet("/", async (ClaimsPrincipal user, QuotingQueries queries, string? status, string? search, int page = 1, int pageSize = 25) =>
        {
            if (!user.TryGetAgencyScope(out var agency)) return Results.Forbid();
            if (status is not null && !Enum.TryParse<QuoteStatus>(status, true, out _))
                return ProblemResults.Validation("status", "Unknown status.");
            pageSize = Math.Clamp(pageSize, 1, 100);
            var list = await queries.QuoteListAsync(agency, status, (Math.Max(page, 1) - 1) * pageSize, pageSize, search);
            return Results.Ok(list);
        }).WithName("ListQuotes").Produces<IReadOnlyList<QuoteSummary>>();

        quotes.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, QuotingDbContext db, CancellationToken ct) =>
        {
            var quote = await db.Quotes.AsNoTracking()
                .Include(q => q.Referrals).Include(q => q.Events).Include(q => q.Agency)
                .AsSplitQuery()
                .FirstOrDefaultAsync(q => q.Id == id, ct);
            if (quote is null || !user.CanSee(quote.AgencyCode)) return Results.NotFound();
            var documents = await db.Documents.AsNoTracking().Where(d => d.QuoteId == id)
                .Select(d => new Data.Entities.StoredDocument { Id = d.Id, DocumentType = d.DocumentType, FileName = d.FileName, CreatedUtc = d.CreatedUtc })
                .ToListAsync(ct);
            return Results.Ok(quote.ToDetail(documents));
        }).WithName("GetQuote").Produces<QuoteDetail>();

        quotes.MapPost("/", (QuoteRequest request, ClaimsPrincipal user, QuoteService service, CancellationToken ct) =>
                service.CreateAsync(request, user, ct))
            .RequireAuthorization(Policies.Broker)
            .WithName("CreateQuote").Produces<QuoteCreated>(StatusCodes.Status202Accepted).ProducesValidationProblem();

        quotes.MapPut("/{id:guid}", (Guid id, RequoteRequest request, ClaimsPrincipal user, QuoteService service, CancellationToken ct) =>
                service.RequoteAsync(id, request, user, ct))
            .WithName("Requote").Produces<QuoteCreated>(StatusCodes.Status202Accepted).ProducesValidationProblem();

        quotes.MapPost("/{id:guid}/bind", (Guid id, BindRequest request, ClaimsPrincipal user, QuoteService service, CancellationToken ct) =>
                service.RequestBindAsync(id, request, user, ct))
            .RequireAuthorization(Policies.Broker)
            .WithName("BindQuote").Produces<QuoteCreated>(StatusCodes.Status202Accepted);

        quotes.MapPost("/{id:guid}/not-taken-up", (Guid id, ClaimsPrincipal user, QuoteService service, CancellationToken ct) =>
                service.NotTakenUpAsync(id, user, ct))
            .WithName("NotTakenUp");

        return api;
    }

    public static RouteGroupBuilder MapReferrals(this RouteGroupBuilder api)
    {
        var referrals = api.MapGroup("/referrals").WithTags("Referrals").RequireAuthorization(Policies.Underwriter);

        referrals.MapGet("/", (QuotingQueries queries) => queries.ReferralQueueAsync())
            .WithName("ReferralQueue");

        referrals.MapPost("/{id:guid}/assign", (Guid id, ClaimsPrincipal user, QuoteService service, CancellationToken ct) =>
            service.AssignAsync(id, user, ct)).WithName("AssignReferral");

        referrals.MapPost("/{id:guid}/decision", (Guid id, ReferralDecisionRequest request, ClaimsPrincipal user, QuoteService service, CancellationToken ct) =>
            service.DecideAsync(id, request, user, ct)).WithName("DecideReferral").Produces<QuoteDetail>();

        return api;
    }
}

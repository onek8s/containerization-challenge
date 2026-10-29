using System.Security.Claims;
using Humongous.Quoting.Api.Infrastructure;
using Humongous.Quoting.Contracts;
using Humongous.Quoting.Contracts.Messages;
using Humongous.Quoting.Data;
using Humongous.Quoting.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Humongous.Quoting.Api.Endpoints;

public static class PolicyEndpoints
{
    public static RouteGroupBuilder MapPolicies(this RouteGroupBuilder api)
    {
        var policies = api.MapGroup("/policies").WithTags("Policies");

        policies.MapGet("/", async (ClaimsPrincipal user, QuotingDbContext db, string? search, string? status, int page = 1, int pageSize = 25) =>
        {
            if (!user.TryGetAgencyScope(out var agency)) return Results.Forbid();
            var query = db.Policies.AsNoTracking();
            if (agency is not null) query = query.Where(p => p.AgencyCode == agency);
            if (status is not null && Enum.TryParse<PolicyStatus>(status, true, out var s)) query = query.Where(p => p.Status == s);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.PolicyNumber.Contains(search) || p.InsuredName.Contains(search) || p.Postcode.Contains(search));
            pageSize = Math.Clamp(pageSize, 1, 100);
            var list = await query.OrderBy(p => p.ExpiryDate)
                .Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize)
                .ToListAsync();
            return Results.Ok(list.Select(p => p.ToSummary()));
        }).WithName("ListPolicies").Produces<IEnumerable<PolicySummary>>();

        policies.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, QuotingDbContext db, CancellationToken ct) =>
        {
            var policy = await db.Policies.AsNoTracking().Include(p => p.Transactions).FirstOrDefaultAsync(p => p.Id == id, ct);
            if (policy is null || !user.CanSee(policy.AgencyCode)) return Results.NotFound();
            var documents = await db.Documents.AsNoTracking().Where(d => d.PolicyId == id)
                .Select(d => new Data.Entities.StoredDocument { Id = d.Id, DocumentType = d.DocumentType, FileName = d.FileName, CreatedUtc = d.CreatedUtc })
                .ToListAsync(ct);
            return Results.Ok(policy.ToDetail(documents));
        }).WithName("GetPolicy").Produces<PolicyDetail>();

        policies.MapPost("/{id:guid}/adjustments", async (Guid id, AdjustmentRequest request, ClaimsPrincipal user,
            QuotingDbContext db, IPublishEndpoint bus, CancellationToken ct) =>
        {
            var policy = await db.Policies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
            if (policy is null || !user.CanSee(policy.AgencyCode)) return Results.NotFound();
            if (policy.Status != PolicyStatus.Live) return ProblemResults.Conflict("Only live policies can be changed.");
            if (request.EffectiveDate < policy.InceptionDate || request.EffectiveDate > policy.ExpiryDate)
                return ProblemResults.Validation("effectiveDate", "The effective date must be within the period of insurance.");
            if (string.IsNullOrWhiteSpace(request.Reason))
                return ProblemResults.Validation("reason", "Give a reason for the change.");
            var validation = await new RiskValidator().ValidateAsync(request.Risk, ct);
            if (!validation.IsValid) return ProblemResults.Validation(validation);

            await bus.Publish(new AdjustmentRequested
            {
                PolicyId = id,
                EffectiveDate = request.EffectiveDate,
                Reason = request.Reason,
                Risk = request.Risk,
                RequestedBy = user.DisplayName()
            }, ct);
            return Results.Accepted($"/api/policies/{id}");
        }).WithName("RequestAdjustment");

        policies.MapPost("/{id:guid}/cancellation", async (Guid id, CancellationRequest request, ClaimsPrincipal user,
            QuotingDbContext db, IPublishEndpoint bus, CancellationToken ct) =>
        {
            var policy = await db.Policies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
            if (policy is null || !user.CanSee(policy.AgencyCode)) return Results.NotFound();
            if (policy.Status != PolicyStatus.Live) return ProblemResults.Conflict("The policy is not live.");
            if (request.EffectiveDate < policy.InceptionDate || request.EffectiveDate > policy.ExpiryDate)
                return ProblemResults.Validation("effectiveDate", "The effective date must be within the period of insurance.");
            await bus.Publish(new CancellationRequested
            {
                PolicyId = id,
                EffectiveDate = request.EffectiveDate,
                Reason = request.Reason,
                RequestedBy = user.DisplayName()
            }, ct);
            return Results.Accepted($"/api/policies/{id}");
        }).WithName("RequestCancellation");

        return api;
    }

    public static RouteGroupBuilder MapDocuments(this RouteGroupBuilder api)
    {
        var documents = api.MapGroup("/documents").WithTags("Documents");

        documents.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, QuotingDbContext db, CancellationToken ct) =>
        {
            var document = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
            if (document is null || !user.CanSee(document.AgencyCode)) return Results.NotFound();
            return Results.File(document.Content, document.ContentType, document.FileName);
        }).WithName("DownloadDocument");

        documents.MapPost("/", async (DocumentRequested request, ClaimsPrincipal user, QuotingDbContext db, IPublishEndpoint bus, CancellationToken ct) =>
        {
            // Reissue a document (e.g. a lost EL certificate).
            var agency = request.PolicyId is { } policyId
                ? await db.Policies.Where(p => p.Id == policyId).Select(p => p.AgencyCode).FirstOrDefaultAsync(ct)
                : await db.Quotes.Where(q => q.Id == request.QuoteId).Select(q => q.AgencyCode).FirstOrDefaultAsync(ct);
            if (agency is null || !user.CanSee(agency)) return Results.NotFound();
            await bus.Publish(request, ct);
            return Results.Accepted();
        }).WithName("RequestDocument");

        return api;
    }
}

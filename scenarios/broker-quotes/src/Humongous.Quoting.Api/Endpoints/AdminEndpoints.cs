using System.Security.Claims;
using Humongous.Quoting.Api.Infrastructure;
using Humongous.Quoting.Contracts;
using Humongous.Quoting.Data;
using Humongous.Quoting.Domain;
using Humongous.Quoting.Rating;
using Microsoft.EntityFrameworkCore;

namespace Humongous.Quoting.Api.Endpoints;

public static class AdminEndpoints
{
    public static RouteGroupBuilder MapCatalogue(this RouteGroupBuilder api)
    {
        api.MapGet("/catalogue", () => new CatalogueInfo
        {
            Trades = TradeList.All.Where(t => t.Acceptance != TradeAcceptance.Decline)
                .Select(t => new TradeInfo(t.Code, t.Description, t.Group, t.Acceptance.ToString())).ToList(),
            Products = ProductCatalogue.Versions
                .Select(v => new ProductVersionInfo(v.Version, v.EffectiveFrom, v.EffectiveTo, v.IptRate, v.MaxCommission, v.PolicyFee)).ToList(),
            PublicLiabilityLimits = PublicLiabilityCover.AvailableLimits
        }).WithTags("Catalogue").WithName("Catalogue");

        api.MapGet("/dashboard", async (ClaimsPrincipal user, QuotingQueries queries, TimeProvider clock) =>
        {
            if (!user.TryGetAgencyScope(out var agency)) return Results.Forbid();
            return Results.Ok(await queries.DashboardAsync(agency, clock.GetUtcNow().UtcDateTime));
        }).WithTags("Dashboard").WithName("Dashboard").Produces<Dashboard>();

        api.MapGet("/rating/status", (IRatingEngineStatus status) => status.GetState())
            .RequireAuthorization(Policies.Underwriter).WithTags("Rating").WithName("RatingStatus");

        return api;
    }

    public static RouteGroupBuilder MapAgencies(this RouteGroupBuilder api)
    {
        var agencies = api.MapGroup("/agencies").WithTags("Agencies");

        agencies.MapGet("/", async (QuotingDbContext db) =>
                (await db.Agencies.AsNoTracking().OrderBy(a => a.Code).ToListAsync()).Select(a => a.ToInfo()))
            .RequireAuthorization(Policies.Underwriter).WithName("ListAgencies");

        agencies.MapGet("/mine", async (ClaimsPrincipal user, QuotingDbContext db) =>
        {
            var code = user.AgencyCode();
            var agency = code is null ? null : await db.Agencies.FindAsync(code);
            return agency is null ? Results.NotFound() : Results.Ok(agency.ToInfo());
        }).WithName("MyAgency");

        agencies.MapPut("/{code}", async (string code, AgencyUpdate update, QuotingDbContext db, ClaimsPrincipal user, ILogger<Program> logger) =>
        {
            var agency = await db.Agencies.FindAsync(code);
            if (agency is null) return Results.NotFound();
            var maxCommission = ProductCatalogue.Versions.Max(v => v.MaxCommission);
            if (update.CommissionRate < 0 || update.CommissionRate > maxCommission)
                return ProblemResults.Validation("commissionRate", $"Commission must be between 0 and {maxCommission:P0}.");
            agency.CommissionRate = update.CommissionRate;
            agency.Active = update.Active;
            agency.DocumentEmail = update.DocumentEmail;
            await db.SaveChangesAsync();
            logger.LogInformation("Agency {Code} updated by {User}", code, user.DisplayName());
            return Results.Ok(agency.ToInfo());
        }).RequireAuthorization(Policies.SeniorUnderwriter).WithName("UpdateAgency");

        var bordereaux = api.MapGroup("/bordereaux").WithTags("Bordereaux").RequireAuthorization(Policies.Underwriter);

        bordereaux.MapGet("/", async (QuotingDbContext db) =>
            await db.BordereauRuns.AsNoTracking().OrderByDescending(b => b.CreatedUtc).Take(36)
                .Select(b => new BordereauRunInfo
                {
                    Id = b.Id,
                    Period = b.Period,
                    CreatedUtc = b.CreatedUtc,
                    Rows = b.Rows,
                    GrossPremium = b.GrossPremium,
                    FileName = b.FileName,
                    Status = b.Status,
                    Error = b.Error
                }).ToListAsync()).WithName("ListBordereaux");

        bordereaux.MapGet("/{id:guid}/file", async (Guid id, QuotingDbContext db) =>
        {
            var run = await db.BordereauRuns.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
            return run?.Content is null
                ? Results.NotFound()
                : Results.File(run.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", run.FileName);
        }).WithName("DownloadBordereau");

        return api;
    }
}

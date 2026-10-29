using System.Security.Claims;
using BatchRelease.Contracts;
using BatchRelease.Core.Intake;
using BatchRelease.Core.Model;
using BatchRelease.Data;

namespace BatchRelease.Api;

public sealed record BatchSummary(
    string BatchNumber, string ProductCode, string ProductName, string SiteCode, string Status,
    DateOnly ManufacturedOn, DateOnly ExpiresOn, decimal Quantity, string Unit,
    DateTime? DispositionUtc, string? CertificateNumber, IReadOnlyList<DecisionSummary> Decisions, IReadOnlyList<DocumentSummary> Documents);

public sealed record DecisionSummary(string Kind, string SignedBy, DateTime SignedUtc, string? CertificateNumber, string? Comment);

public sealed record DocumentSummary(string Kind, string FileName, long Length, string Sha256, DateTime StoredUtc);

public sealed record DispositionItem(string BatchNumber, string ProductCode, string SiteCode, string Status, DateOnly ExpiresOn, DateTime DispositionUtc, string? CertificateNumber);

public static class BatchEndpoints
{
    public static void MapBatchEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");

        api.MapPost("/batches", async (BatchSubmission submission, BatchIntake intake, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var result = await intake.SubmitAsync(submission, user.FindFirstValue(ApiKeyAuthenticationHandler.SiteClaim), ct);
            return result.Outcome switch
            {
                IntakeOutcome.Created => Results.Created($"/api/v1/batches/{submission.BatchNumber}", new { submission.BatchNumber, status = "Received" }),
                IntakeOutcome.Updated => Results.Ok(new { submission.BatchNumber, status = "Updated" }),
                _ => Results.ValidationProblem(new Dictionary<string, string[]> { ["submission"] = result.Errors.ToArray() }),
            };
        }).RequireAuthorization("Submit");

        api.MapPost("/batches/{batchNumber}/documents", async (string batchNumber, HttpRequest request, BatchQueries queries, ISigningHostClient signing, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest("multipart/form-data expected.");
            var form = await request.ReadFormAsync(ct);
            var file = form.Files["file"];
            var kind = form["kind"].ToString();
            if (file is null || file.Length == 0)
                return Results.BadRequest("file is required.");
            if (!DocumentKinds.IsKnown(kind))
                return Results.BadRequest($"kind must be one of {string.Join(", ", DocumentKinds.All)}.");

            var batch = await queries.GetByNumberAsync(batchNumber, ct);
            if (batch is null || !SiteAllowed(user, batch.SiteCode))
                return Results.NotFound();

            await using var content = file.OpenReadStream();
            try
            {
                var stored = await signing.StoreDocumentAsync(batch.Id, kind, file.FileName, file.ContentType, content, user.Identity!.Name!, ct);
                return Results.Created($"/api/v1/batches/{batchNumber}", stored);
            }
            catch (SigningHostException ex) when (ex.Status == System.Net.HttpStatusCode.Conflict)
            {
                return Results.Conflict(ex.Message);
            }
        }).RequireAuthorization("Submit").DisableAntiforgery();

        api.MapGet("/batches/{batchNumber}", async (string batchNumber, BatchQueries queries, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var batch = await queries.GetByNumberAsync(batchNumber, ct);
            if (batch is null || !SiteAllowed(user, batch.SiteCode))
                return Results.NotFound();
            return Results.Ok(ToSummary(batch));
        }).RequireAuthorization("Read");

        api.MapGet("/batches/{batchNumber}/certificate", async (string batchNumber, BatchQueries queries, ISigningHostClient signing, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var batch = await queries.GetByNumberAsync(batchNumber, ct);
            if (batch is null || !SiteAllowed(user, batch.SiteCode))
                return Results.NotFound();
            if (batch.CertificateNumber is null)
                return Results.Problem($"Batch {batchNumber} has no certificate yet (status {batch.Status}).", statusCode: StatusCodes.Status409Conflict);

            var bytes = await signing.GetCertificateAsync(batch.CertificateNumber, ct);
            return bytes is null
                ? Results.NotFound()
                : Results.File(bytes, "application/pkcs7-mime", batch.CertificateNumber + ".p7s");
        }).RequireAuthorization("Read");

        api.MapGet("/dispositions", async (DateTime? since, BatchQueries queries, ClaimsPrincipal user, TimeProvider clock, CancellationToken ct) =>
        {
            var from = since?.ToUniversalTime() ?? clock.GetUtcNow().UtcDateTime.AddDays(-7);
            var batches = await queries.DispositionsSinceAsync(from, user.FindFirstValue(ApiKeyAuthenticationHandler.SiteClaim), ct);
            return Results.Ok(batches
                .Where(b => b.Status is BatchStatus.Released or BatchStatus.Rejected)
                .Select(b => new DispositionItem(b.BatchNumber, b.ProductCode, b.SiteCode, b.Status.ToString(), b.ExpiresOn, b.DispositionUtc!.Value, b.CertificateNumber)));
        }).RequireAuthorization("Read");
    }

    private static bool SiteAllowed(ClaimsPrincipal user, string siteCode)
    {
        var site = user.FindFirstValue(ApiKeyAuthenticationHandler.SiteClaim);
        return site is null || string.Equals(site, siteCode, StringComparison.OrdinalIgnoreCase);
    }

    internal static BatchSummary ToSummary(Batch batch) => new(
        batch.BatchNumber, batch.ProductCode, batch.Product?.Name ?? "", batch.SiteCode, batch.Status.ToString(),
        batch.ManufacturedOn, batch.ExpiresOn, batch.Quantity, batch.Unit, batch.DispositionUtc, batch.CertificateNumber,
        batch.Decisions.OrderBy(d => d.SignedUtc).Select(d => new DecisionSummary(d.Kind.ToString(), d.SignerName, d.SignedUtc, d.CertificateNumber, d.Comment)).ToList(),
        batch.Documents.OrderBy(d => d.StoredUtc).Select(d => new DocumentSummary(d.Kind, d.FileName, d.Length, d.Sha256, d.StoredUtc)).ToList());
}

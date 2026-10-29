using System.Net;
using System.Security.Principal;
using BatchRelease.Contracts;
using BatchRelease.Core.Model;
using BatchRelease.Core.Rules;
using BatchRelease.Core.Security;
using BatchRelease.Data;
using BatchRelease.SigningHost.Signing;
using BatchRelease.Vault;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BatchRelease.SigningHost.Endpoints;

public static class SigningEndpoints
{
    private const long MaxDocumentBytes = 200L * 1024 * 1024;

    public static void MapSigningEndpoints(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/v1").RequireAuthorization(TrustedCallerRequirement.Policy);

        v1.MapPost("/decisions", async (DecisionRequest request, DecisionService decisions, HttpContext http, CancellationToken ct) =>
        {
            var outcome = await decisions.DecideAsync(request, http.User.Identity!.Name!, ct);
            return outcome.Response is not null
                ? Results.Json(outcome.Response, SigningHostClient.Json)
                : Results.Json(outcome.Problem, SigningHostClient.Json, "application/problem+json", (int)outcome.Status);
        });

        v1.MapGet("/access/{batchId:int}", async (int batchId, string sid, BatchReleaseContext db, AccessEvaluator access, CancellationToken ct) =>
        {
            var policy = await PolicyForAsync(db, batchId, ct);
            if (policy is null)
                return Results.NotFound();
            if (!TryParseSid(sid, out var user))
                return Results.BadRequest("sid is not a valid SID.");

            var granted = access.GrantedRights(user, policy);
            return Results.Json(new AccessResponse
            {
                BatchId = batchId,
                Sid = user.Value,
                GrantedMask = (uint)granted,
                Rights = granted.Names().ToList(),
            }, SigningHostClient.Json);
        });

        v1.MapPost("/batches/{batchId:int}/documents", async (int batchId, HttpRequest request, BatchReleaseContext db, RecordVault vault, TimeProvider clock, CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest("multipart/form-data expected.");
            var form = await request.ReadFormAsync(ct);
            var file = form.Files["file"];
            var kind = form["kind"].ToString();
            var submittedBy = form["submittedBy"].ToString();
            if (file is null || file.Length == 0)
                return Results.BadRequest("file is required.");
            if (file.Length > MaxDocumentBytes)
                return Results.BadRequest("file is too large.");
            if (!DocumentKinds.IsKnown(kind))
                return Results.BadRequest($"kind must be one of {string.Join(", ", DocumentKinds.All)}.");

            var batch = await db.Batches.Include(b => b.Documents).FirstOrDefaultAsync(b => b.Id == batchId, ct);
            if (batch is null)
                return Results.NotFound();
            if (batch.Status is not (BatchStatus.Received or BatchStatus.UnderReview or BatchStatus.OnHold))
                return Results.Conflict($"Batch {batch.BatchNumber} is {batch.Status}; documents can no longer be added.");

            await using var content = file.OpenReadStream();
            var stored = await vault.StoreAsync(batch.SiteCode, batch.BatchNumber, kind, file.FileName, content, ct);
            var document = new BatchDocument
            {
                Kind = DocumentKinds.All.First(k => string.Equals(k, kind, StringComparison.OrdinalIgnoreCase)),
                FileName = Path.GetFileName(file.FileName),
                ContentType = string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType,
                Length = stored.Length,
                Sha256 = stored.Sha256,
                VaultPath = stored.RelativePath,
                StoredUtc = clock.GetUtcNow().UtcDateTime,
                StoredBy = string.IsNullOrWhiteSpace(submittedBy) ? request.HttpContext.User.Identity!.Name! : submittedBy,
            };
            batch.Documents.Add(document);
            if (StatusTransitions.ReadyForReview(batch))
                batch.Status = BatchStatus.UnderReview;
            await db.SaveChangesAsync(ct);

            return Results.Json(new DocumentStored
            {
                DocumentId = document.Id,
                Kind = document.Kind,
                FileName = document.FileName,
                Length = document.Length,
                Sha256 = document.Sha256,
            }, SigningHostClient.Json);
        }).DisableAntiforgery();

        v1.MapGet("/batches/{batchId:int}/documents/{documentId:int}", async (
            int batchId, int documentId, string sid, BatchReleaseContext db, AccessEvaluator access, RecordVault vault, SecurityAudit audit, CancellationToken ct) =>
        {
            var document = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentId && d.BatchId == batchId, ct);
            var policy = await PolicyForAsync(db, batchId, ct);
            if (document is null || policy is null)
                return Results.NotFound();
            if (!TryParseSid(sid, out var user))
                return Results.BadRequest("sid is not a valid SID.");

            var batchNumber = await db.Batches.Where(b => b.Id == batchId).Select(b => b.BatchNumber).FirstAsync(ct);
            if (!access.GrantedRights(user, policy).HasFlag(ReleaseRights.ReadRecord))
            {
                audit.Failure(AuditEvent.RecordAccessDenied, user, batchNumber, document.FileName);
                return Results.StatusCode((int)HttpStatusCode.Forbidden);
            }

            try
            {
                var stream = await vault.OpenVerifiedAsync(document.VaultPath, document.Sha256, ct);
                audit.Success(AuditEvent.RecordOpened, user, batchNumber, document.FileName);
                return Results.Stream(stream, document.ContentType, document.FileName);
            }
            catch (VaultIntegrityException ex)
            {
                audit.Failure(AuditEvent.RecordIntegrityFailure, user, batchNumber, document.FileName, ex.Message);
                return Results.Problem(ex.Message, statusCode: (int)HttpStatusCode.Conflict, title: "Record integrity check failed");
            }
        });

        v1.MapGet("/certificates/{number}", async (string number, BatchReleaseContext db, RecordVault vault, CancellationToken ct) =>
        {
            var record = await db.Certificates.AsNoTracking().FirstOrDefaultAsync(c => c.CertificateNumber == number, ct);
            if (record is null)
                return Results.NotFound();
            var bytes = await vault.ReadVerifiedAsync(record.VaultPath, record.Sha256, ct);
            return Results.File(bytes, "application/pkcs7-mime", number + ".p7s");
        });

        v1.MapGet("/status", async (IOptions<SigningOptions> options, RecordVault vault, SecurityAudit audit, BatchReleaseContext db, CancellationToken ct) =>
            Results.Json(new SigningHostStatus
            {
                MachineName = Environment.MachineName,
                KeyProvider = options.Value.KeyStorageProvider,
                RequireHardwareKeys = options.Value.RequireHardwareKeys,
                VaultEncrypted = vault.RootEncrypted,
                VaultRoot = vault.Root,
                SecurityAuditing = audit.Enabled,
                ActiveSigners = await db.Signers.CountAsync(s => s.Active, ct),
                Version = typeof(SigningEndpoints).Assembly.GetName().Version?.ToString() ?? "",
            }, SigningHostClient.Json));
    }

    private static Task<string?> PolicyForAsync(BatchReleaseContext db, int batchId, CancellationToken ct) =>
        db.Batches.Where(b => b.Id == batchId).Select(b => b.Product!.Family!.PolicySddl).FirstOrDefaultAsync(ct);

    private static bool TryParseSid(string? value, out SecurityIdentifier sid)
    {
        sid = null!;
        try
        {
            sid = new SecurityIdentifier(value ?? "");
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

using System.Net;
using System.Security.Principal;
using BatchRelease.Contracts;
using BatchRelease.Core.Certificates;
using BatchRelease.Core.Model;
using BatchRelease.Core.Rules;
using BatchRelease.Core.Security;
using BatchRelease.Data;
using BatchRelease.Vault;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BatchRelease.SigningHost.Signing;

public sealed record DecisionOutcome(HttpStatusCode Status, DecisionResponse? Response, DecisionProblem? Problem)
{
    public static DecisionOutcome Fail(HttpStatusCode status, string code, string message, IEnumerable<Finding>? findings = null) =>
        new(status, null, new DecisionProblem { Code = code, Message = message, Findings = findings?.ToList() ?? new() });
}

/// <summary>
/// Signs review, release, reject and hold decisions. Every decision is authenticated with the
/// signer's password, authorized against the product family's release policy with the
/// signer's logon token, checked against the disposition rules, and audited.
/// </summary>
public sealed class DecisionService
{
    private static readonly SemaphoreSlim CertificateNumberLock = new(1, 1);

    private readonly BatchReleaseContext _db;
    private readonly BatchQueries _queries;
    private readonly CredentialVerifier _credentials;
    private readonly AccessEvaluator _access;
    private readonly SigningKeyStore _keys;
    private readonly RecordVault _vault;
    private readonly SecurityAudit _audit;
    private readonly TimeProvider _clock;
    private readonly SigningOptions _options;
    private readonly ILogger<DecisionService> _logger;

    public DecisionService(
        BatchReleaseContext db, BatchQueries queries, CredentialVerifier credentials, AccessEvaluator access,
        SigningKeyStore keys, RecordVault vault, SecurityAudit audit, TimeProvider clock,
        IOptions<SigningOptions> options, ILogger<DecisionService> logger)
    {
        _db = db;
        _queries = queries;
        _credentials = credentials;
        _access = access;
        _keys = keys;
        _vault = vault;
        _audit = audit;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DecisionOutcome> DecideAsync(DecisionRequest request, string caller, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Kind))
            return DecisionOutcome.Fail(HttpStatusCode.BadRequest, "invalid-kind", $"Unknown decision '{request.Kind}'.");
        if (!request.MeaningConfirmed)
            return DecisionOutcome.Fail(HttpStatusCode.BadRequest, "meaning-not-confirmed", "The signer must confirm the meaning of the signature.");
        if (SignatureMeanings.RequiresComment(request.Kind) && string.IsNullOrWhiteSpace(request.Comment))
            return DecisionOutcome.Fail(HttpStatusCode.BadRequest, "comment-required", $"A reason is required to {request.Kind.ToString().ToLowerInvariant()} a batch.");
        if (string.IsNullOrWhiteSpace(request.AccountName) || string.IsNullOrEmpty(request.Password))
            return DecisionOutcome.Fail(HttpStatusCode.BadRequest, "credentials-required", "Account name and password are required.");

        var batch = await _queries.GetAsync(request.BatchId, ct);
        if (batch is null)
            return DecisionOutcome.Fail(HttpStatusCode.NotFound, "batch-not-found", $"Batch {request.BatchId} does not exist.");
        var policy = batch.Product?.Family?.PolicySddl;
        if (string.IsNullOrWhiteSpace(policy))
            return DecisionOutcome.Fail(HttpStatusCode.Conflict, "no-policy", $"Product {batch.ProductCode} has no release policy.");

        using var logon = _credentials.Logon(request.AccountName, request.Password, out var failure);
        if (logon is null)
        {
            _audit.Failure(AuditEvent.SignatureCredentialsRejected, null, request.AccountName, batch.BatchNumber, request.Kind.ToString(), failure!.Reason, caller, request.Workstation ?? "");
            return DecisionOutcome.Fail(HttpStatusCode.Unauthorized, "signature-rejected", failure.Reason);
        }

        var granted = _access.GrantedRights(logon.Token, policy);
        var required = ReleaseRightsExtensions.RequiredFor(request.Kind);
        if ((granted & required) != required)
        {
            _audit.Failure(AuditEvent.DecisionDenied, logon.Sid, logon.AccountName, batch.BatchNumber, request.Kind.ToString(), $"granted {granted}", caller, request.Workstation ?? "");
            return DecisionOutcome.Fail(HttpStatusCode.Forbidden, "not-authorized",
                $"{logon.AccountName} is not authorized to {request.Kind.ToString().ToLowerInvariant()} {batch.Product!.Family!.Name} batches.");
        }

        var assessment = new DispositionRules(_clock).Assess(batch, request.Kind, logon.Sid.Value);
        if (!assessment.CanProceed)
        {
            _audit.Failure(AuditEvent.DecisionDenied, logon.Sid, logon.AccountName, batch.BatchNumber, request.Kind.ToString(),
                string.Join("; ", assessment.Blocking.Select(f => f.Code)), caller, request.Workstation ?? "");
            return DecisionOutcome.Fail(HttpStatusCode.UnprocessableEntity, "rules-not-met", "The batch does not meet the conditions for this decision.", assessment.Findings);
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var decision = new Decision
        {
            BatchId = batch.Id,
            Kind = request.Kind,
            SignerAccount = logon.AccountName,
            SignerSid = logon.Sid.Value,
            SignerName = logon.AccountName,
            Meaning = SignatureMeanings.For(request.Kind),
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            SignedUtc = now,
        };

        if (SignatureMeanings.RequiresSigningKey(request.Kind))
        {
            var signer = await _db.Signers.FirstOrDefaultAsync(s => s.Sid == logon.Sid.Value && s.Active, ct);
            if (signer is null || signer.ValidFromUtc > now || signer.ValidToUtc < now)
            {
                _audit.Failure(AuditEvent.DecisionDenied, logon.Sid, logon.AccountName, batch.BatchNumber, request.Kind.ToString(), "no valid signer enrolment", caller, request.Workstation ?? "");
                return DecisionOutcome.Fail(HttpStatusCode.Conflict, "signer-not-enrolled", $"{logon.AccountName} has no valid signing key on this host.");
            }
            if (_options.RequireHardwareKeys && !signer.HardwareKey)
                return DecisionOutcome.Fail(HttpStatusCode.Conflict, "software-key", $"The signing key of {signer.DisplayName} is not a hardware key.");

            decision.SignerName = signer.DisplayName;
            decision.SignerId = signer.Id;

            var integrity = await VerifyDocumentsAsync(batch, logon.Sid, ct);
            if (integrity is not null)
                return integrity;

            await CertificateNumberLock.WaitAsync(ct);
            try
            {
                var number = await NextCertificateNumberAsync(request.Kind == DecisionKind.Release, batch.SiteCode, now.Year, ct);
                var content = BuildContent(batch, decision, signer, number);
                var payload = CertificateSerializer.Serialize(content);
                var signed = _keys.Sign(logon.Token, signer.KeyName, signer.CertificateDer, payload, now);
                var stored = _vault.StoreBytes(batch.SiteCode, batch.BatchNumber, "BatchCertificate", number + ".p7s", signed);

                decision.CertificateNumber = number;
                decision.SignatureSha256 = stored.Sha256;
                batch.CertificateNumber = number;
                batch.Status = StatusTransitions.After(batch, request.Kind);
                batch.DispositionUtc = now;
                batch.Decisions.Add(decision);

                var strategy = _db.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await _db.Database.BeginTransactionAsync(ct);
                    await _db.SaveChangesAsync(ct);
                    _db.Certificates.Add(new ReleaseCertificateRecord
                    {
                        CertificateNumber = number,
                        BatchId = batch.Id,
                        DecisionId = decision.Id,
                        SignerThumbprint = signer.Thumbprint,
                        VaultPath = stored.RelativePath,
                        Sha256 = stored.Sha256,
                        IssuedUtc = now,
                    });
                    await _db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return DecisionOutcome.Fail(HttpStatusCode.Conflict, "concurrent-change", "The batch was changed by someone else. Reload and sign again.");
            }
            finally
            {
                CertificateNumberLock.Release();
            }
        }
        else
        {
            batch.Decisions.Add(decision);
            batch.Status = StatusTransitions.After(batch, request.Kind);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return DecisionOutcome.Fail(HttpStatusCode.Conflict, "concurrent-change", "The batch was changed by someone else. Reload and sign again.");
            }
        }

        _audit.Success(AuditEvent.DecisionSigned, logon.Sid, logon.AccountName, batch.BatchNumber, request.Kind.ToString(),
            decision.CertificateNumber ?? "", decision.Meaning, caller, request.Workstation ?? "");
        _logger.LogInformation("{Kind} of batch {Batch} signed by {Account} ({Certificate})", request.Kind, batch.BatchNumber, logon.AccountName, decision.CertificateNumber);

        return new DecisionOutcome(HttpStatusCode.OK, new DecisionResponse
        {
            DecisionId = decision.Id,
            Kind = decision.Kind,
            BatchStatus = batch.Status,
            CertificateNumber = decision.CertificateNumber,
            SignedUtc = now,
            Warnings = assessment.Findings.Where(f => f.Severity == FindingSeverity.Warning).ToList(),
        }, null);
    }

    private async Task<DecisionOutcome?> VerifyDocumentsAsync(Batch batch, SecurityIdentifier signer, CancellationToken ct)
    {
        foreach (var document in batch.Documents)
        {
            try
            {
                await using var _ = await _vault.OpenVerifiedAsync(document.VaultPath, document.Sha256, ct);
            }
            catch (Exception ex) when (ex is VaultIntegrityException or FileNotFoundException or DirectoryNotFoundException)
            {
                _audit.Failure(AuditEvent.RecordIntegrityFailure, signer, batch.BatchNumber, document.FileName, ex.Message);
                return DecisionOutcome.Fail(HttpStatusCode.Conflict, "record-integrity", $"Document {document.FileName} failed the integrity check. QA must investigate before signing.");
            }
        }
        return null;
    }

    private async Task<string> NextCertificateNumberAsync(bool release, string siteCode, int year, CancellationToken ct)
    {
        var prefix = CertificateNumbers.Format(release, siteCode, year, 1)[..^5];
        var last = await _db.Certificates.Where(c => c.CertificateNumber.StartsWith(prefix))
            .OrderByDescending(c => c.CertificateNumber).Select(c => c.CertificateNumber).FirstOrDefaultAsync(ct);
        var next = 1;
        if (last is not null && CertificateNumbers.TryParse(last, out _, out _, out _, out var seq))
            next = seq + 1;
        return CertificateNumbers.Format(release, siteCode, year, next);
    }

    private ReleaseCertificateContent BuildContent(Batch batch, Decision decision, Signer signer, string number)
    {
        var review = batch.Decisions.FirstOrDefault(d => d.Kind == DecisionKind.Review);
        var product = batch.Product!;
        return new ReleaseCertificateContent
        {
            CertificateNumber = number,
            Decision = decision.Kind == DecisionKind.Release ? "RELEASED" : "REJECTED",
            Manufacturer = _options.Manufacturer,
            SiteCode = batch.SiteCode,
            BatchNumber = batch.BatchNumber,
            ProductCode = product.Code,
            ProductName = product.Name,
            Strength = product.Strength,
            DosageForm = product.DosageForm,
            Quantity = batch.Quantity,
            Unit = batch.Unit,
            ManufacturedOn = batch.ManufacturedOn,
            ExpiresOn = batch.ExpiresOn,
            QualifiedPerson = signer.DisplayName,
            QualifiedPersonAccount = signer.AccountName,
            Meaning = decision.Meaning,
            Comment = decision.Comment,
            SignedUtc = decision.SignedUtc,
            ReviewedBy = review?.SignerName ?? "",
            ReviewedUtc = review?.SignedUtc,
            Documents = batch.Documents.Select(d => new CertificateDocument(d.Kind, d.FileName, d.Sha256)).ToList(),
            Deviations = batch.Deviations.Select(d => d.Number).ToList(),
            KeyProvider = signer.KeyProvider,
            HardwareKey = signer.HardwareKey,
        };
    }
}

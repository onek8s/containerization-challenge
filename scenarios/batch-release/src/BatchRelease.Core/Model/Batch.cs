namespace BatchRelease.Core.Model;

public class Batch
{
    public int Id { get; set; }
    public string BatchNumber { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public Product? Product { get; set; }
    public string SiteCode { get; set; } = "";
    public string MesReference { get; set; } = "";
    public DateOnly ManufacturedOn { get; set; }
    public DateOnly ExpiresOn { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "";
    public BatchStatus Status { get; set; }
    public DateTime ReceivedUtc { get; set; }
    public DateTime? DispositionUtc { get; set; }
    public string? CertificateNumber { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public List<RecordStep> Steps { get; set; } = new();
    public List<LabResult> Results { get; set; } = new();
    public List<Deviation> Deviations { get; set; } = new();
    public List<BatchDocument> Documents { get; set; } = new();
    public List<Decision> Decisions { get; set; } = new();
}

public class RecordStep
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public int Sequence { get; set; }
    public string Operation { get; set; } = "";
    public StepStatus Status { get; set; }
    public string? PerformedBy { get; set; }
    public string? VerifiedBy { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public string? Comment { get; set; }
}

public class LabResult
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public string TestCode { get; set; } = "";
    public string TestName { get; set; } = "";
    public string Specification { get; set; } = "";
    public decimal? LowerLimit { get; set; }
    public decimal? UpperLimit { get; set; }
    public decimal? Value { get; set; }
    public string? TextResult { get; set; }
    public string Unit { get; set; } = "";
    public ResultOutcome Outcome { get; set; }
    public string? InvestigationNumber { get; set; }
    public bool InvestigationClosed { get; set; }
    public string? ApprovedBy { get; set; }
}

public class Deviation
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public string Number { get; set; } = "";
    public string Title { get; set; } = "";
    public DeviationClass Classification { get; set; }
    public DeviationStatus Status { get; set; }
    public ProductImpact Impact { get; set; }
    public string? CapaNumber { get; set; }
}

public class BatchDocument
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public string Kind { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Length { get; set; }
    public string Sha256 { get; set; } = "";
    public string VaultPath { get; set; } = "";
    public DateTime StoredUtc { get; set; }
    public string StoredBy { get; set; } = "";
}

public class Decision
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public DecisionKind Kind { get; set; }
    public string SignerAccount { get; set; } = "";
    public string SignerSid { get; set; } = "";
    public string SignerName { get; set; } = "";
    public string Meaning { get; set; } = "";
    public string? Comment { get; set; }
    public DateTime SignedUtc { get; set; }
    public int? SignerId { get; set; }
    public string? CertificateNumber { get; set; }
    public string? SignatureSha256 { get; set; }
}

public class Signer
{
    public int Id { get; set; }
    public string AccountName { get; set; } = "";
    public string Sid { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string KeyName { get; set; } = "";
    public string KeyProvider { get; set; } = "";
    public bool HardwareKey { get; set; }
    public string Thumbprint { get; set; } = "";
    public byte[] CertificateDer { get; set; } = Array.Empty<byte>();
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public DateTime EnrolledUtc { get; set; }
    public string EnrolledBy { get; set; } = "";
    public bool Active { get; set; } = true;
}

public class ReleaseCertificateRecord
{
    public int Id { get; set; }
    public string CertificateNumber { get; set; } = "";
    public int BatchId { get; set; }
    public int DecisionId { get; set; }
    public string SignerThumbprint { get; set; } = "";
    public string VaultPath { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public DateTime IssuedUtc { get; set; }
}

public class ApiClient
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string KeyHash { get; set; } = "";
    public string KeyPrefix { get; set; } = "";
    public bool CanSubmit { get; set; }
    public bool CanRead { get; set; }
    public string? SiteCode { get; set; }
    public bool Active { get; set; } = true;
    public DateTime? LastUsedUtc { get; set; }
}

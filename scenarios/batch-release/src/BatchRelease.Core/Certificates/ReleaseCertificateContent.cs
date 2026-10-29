namespace BatchRelease.Core.Certificates;

/// <summary>
/// Content of a batch certificate (release or rejection). It is serialized with
/// <see cref="CertificateSerializer"/> and signed with the Qualified Person's key.
/// </summary>
public sealed class ReleaseCertificateContent
{
    public string SchemaVersion { get; init; } = "2";
    public string CertificateNumber { get; init; } = "";
    public string Decision { get; init; } = "";
    public string Manufacturer { get; init; } = "";
    public string SiteCode { get; init; } = "";
    public string BatchNumber { get; init; } = "";
    public string ProductCode { get; init; } = "";
    public string ProductName { get; init; } = "";
    public string Strength { get; init; } = "";
    public string DosageForm { get; init; } = "";
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = "";
    public DateOnly ManufacturedOn { get; init; }
    public DateOnly ExpiresOn { get; init; }
    public string QualifiedPerson { get; init; } = "";
    public string QualifiedPersonAccount { get; init; } = "";
    public string Meaning { get; init; } = "";
    public string? Comment { get; init; }
    public DateTime SignedUtc { get; init; }
    public string ReviewedBy { get; init; } = "";
    public DateTime? ReviewedUtc { get; init; }
    public IReadOnlyList<CertificateDocument> Documents { get; init; } = Array.Empty<CertificateDocument>();
    public IReadOnlyList<string> Deviations { get; init; } = Array.Empty<string>();
    public string KeyProvider { get; init; } = "";
    public bool HardwareKey { get; init; }
}

public sealed record CertificateDocument(string Kind, string FileName, string Sha256);

using Litware.DocControl.Documents;
using Litware.DocControl.Transmittals;

namespace Litware.DocControl.Data;

public sealed class Project
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Client { get; set; } = "";
    public bool Active { get; set; }
}

public sealed class DocumentRecord
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public string ProjectCode { get; set; } = "";
    public string Discipline { get; set; } = "";
    public string DocumentType { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public string CreatedBy { get; set; } = "";

    // Latest revision, filled by register queries
    public string? LatestRevision { get; set; }
    public RevisionStatus? LatestStatus { get; set; }
    public string? ApprovedRevision { get; set; }
}

public sealed class RevisionRecord
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string DocumentNumber { get; set; } = "";
    public string DocumentTitle { get; set; } = "";
    public string ProjectCode { get; set; } = "";
    public string Code { get; set; } = "";
    public RevisionStatus Status { get; set; }
    public string FileName { get; set; } = "";
    public string VaultPath { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
    public string Source { get; set; } = "";
    public string SubmittedBy { get; set; } = "";
    public DateTime SubmittedUtc { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedUtc { get; set; }
    public string? ReviewComment { get; set; }
}

public sealed class TransmittalRecord
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public string ProjectCode { get; set; } = "";
    public TransmittalPurpose Purpose { get; set; }
    public string RecipientCompany { get; set; } = "";
    public string RecipientName { get; set; } = "";
    public string RecipientEmail { get; set; } = "";
    public string? Notes { get; set; }
    public TransmittalStatus Status { get; set; }
    public string CreatedBy { get; set; } = "";
    public string CreatedByEmail { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime? IssuedUtc { get; set; }
    public string? PackagePath { get; set; }
    public long? PackageSize { get; set; }
    public string? Error { get; set; }
    public int Attempts { get; set; }
    public string? ClaimedBy { get; set; }
    public DateTime? ClaimedUtc { get; set; }
    public int ItemCount { get; set; }
}

public sealed class DropFileRecord
{
    public int Id { get; set; }
    public string FileId { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string OriginalPath { get; set; } = "";
    public string Result { get; set; } = "";
    public string? Reason { get; set; }
    public int? RevisionId { get; set; }
    public DateTime ProcessedUtc { get; set; }
}

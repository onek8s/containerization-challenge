using Litware.DocControl.Data;
using Litware.DocControl.Transmittals;

namespace Litware.DocControl.Web.Models;

public sealed class DashboardViewModel
{
    public IReadOnlyList<Project> Projects { get; init; } = [];
    public IReadOnlyList<RevisionRecord> PendingReview { get; init; } = [];
    public IReadOnlyList<TransmittalRecord> RecentTransmittals { get; init; } = [];
}

public sealed class RegisterViewModel
{
    public IReadOnlyList<Project> Projects { get; init; } = [];
    public string Project { get; init; } = "";
    public string? Discipline { get; init; }
    public string? Search { get; init; }
    public IReadOnlyList<DocumentRecord> Documents { get; init; } = [];
}

public sealed class DocumentViewModel
{
    public DocumentRecord Document { get; init; } = new();
    public IReadOnlyList<RevisionRecord> Revisions { get; init; } = [];
    public string SuggestedRevision { get; init; } = "";
}

public sealed class UploadViewModel
{
    public string DocumentNumber { get; set; } = "";
    public string Revision { get; set; } = "";
    public string? Title { get; set; }
    public IFormFile? File { get; set; }
}

public sealed class CreateTransmittalViewModel
{
    public IReadOnlyList<Project> Projects { get; set; } = [];
    public IReadOnlyList<RevisionRecord> Issuable { get; set; } = [];
    public string Project { get; set; } = "";
    public TransmittalPurpose Purpose { get; set; } = TransmittalPurpose.ForInformation;
    public string RecipientCompany { get; set; } = "";
    public string? RecipientName { get; set; }
    public string RecipientEmail { get; set; } = "";
    public string? Notes { get; set; }
    public List<int> RevisionIds { get; set; } = new();
}

public sealed class TransmittalDetailsViewModel
{
    public TransmittalRecord Transmittal { get; init; } = new();
    public IReadOnlyList<RevisionRecord> Items { get; init; } = [];
    public bool PackageAvailable { get; init; }
}

public sealed class DiagnosticsViewModel
{
    public List<(string Name, string Value)> Rows { get; } = new();
}

public sealed class ErrorViewModel
{
    public string? RequestId { get; init; }
}

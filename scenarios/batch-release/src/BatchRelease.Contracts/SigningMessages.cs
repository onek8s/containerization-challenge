using BatchRelease.Core.Model;
using BatchRelease.Core.Rules;

namespace BatchRelease.Contracts;

/// <summary>
/// Decision signed with the user's account and password (21 CFR 11.200 two-component signature).
/// </summary>
public sealed class DecisionRequest
{
    public int BatchId { get; set; }
    public DecisionKind Kind { get; set; }
    public string AccountName { get; set; } = "";
    public string Password { get; set; } = "";
    public bool MeaningConfirmed { get; set; }
    public string? Comment { get; set; }
    public string? Workstation { get; set; }
}

public sealed class DecisionResponse
{
    public int DecisionId { get; set; }
    public DecisionKind Kind { get; set; }
    public BatchStatus BatchStatus { get; set; }
    public string? CertificateNumber { get; set; }
    public DateTime SignedUtc { get; set; }
    public List<Finding> Warnings { get; set; } = new();
}

public sealed class DecisionProblem
{
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public List<Finding> Findings { get; set; } = new();
}

public sealed class AccessResponse
{
    public int BatchId { get; set; }
    public string Sid { get; set; } = "";
    public uint GrantedMask { get; set; }
    public List<string> Rights { get; set; } = new();
}

public sealed class DocumentStored
{
    public int DocumentId { get; set; }
    public string Kind { get; set; } = "";
    public string FileName { get; set; } = "";
    public long Length { get; set; }
    public string Sha256 { get; set; } = "";
}

public sealed class SigningHostStatus
{
    public string MachineName { get; set; } = "";
    public string KeyProvider { get; set; } = "";
    public bool RequireHardwareKeys { get; set; }
    public bool VaultEncrypted { get; set; }
    public string VaultRoot { get; set; } = "";
    public bool SecurityAuditing { get; set; }
    public int ActiveSigners { get; set; }
    public string Version { get; set; } = "";
}

public static class DocumentKinds
{
    public const string ExecutedBatchRecord = "ExecutedBatchRecord";
    public const string CertificateOfAnalysis = "CertificateOfAnalysis";
    public const string DeviationReport = "DeviationReport";
    public const string Other = "Other";

    public static readonly IReadOnlyList<string> All = new[] { ExecutedBatchRecord, CertificateOfAnalysis, DeviationReport, Other };

    public static bool IsKnown(string? kind) => kind is not null && All.Contains(kind, StringComparer.OrdinalIgnoreCase);
}

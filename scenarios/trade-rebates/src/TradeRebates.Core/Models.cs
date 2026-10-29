namespace ContosoFoods.TradeRebates.Core;

public static class ClaimStatus
{
    public const string Received = "R";
    public const string Validated = "V";
    public const string Rejected = "X";
    public const string Approved = "A";
    public const string Cancelled = "C";

    public static string Name(string status) => status switch
    {
        Received => "Received",
        Validated => "To review",
        Rejected => "Rejected",
        Approved => "Approved",
        Cancelled => "Cancelled",
        _ => status
    };
}

public static class LineStatus
{
    public static string Name(string status) => status switch
    {
        "P" => "Pending",
        "K" => "OK",
        "W" => "Warning",
        "X" => "Rejected",
        _ => status
    };
}

public static class AgreementStatus
{
    public static string Name(string status) => status switch
    {
        "D" => "Draft",
        "A" => "Active",
        "C" => "Closed",
        _ => status
    };

    public static string BasisName(string basis) => basis == "Q" ? "Quantity" : "Net value";
}

public static class StatementStatus
{
    public static string Name(string status) => status switch
    {
        "Q" => "Queued",
        "R" => "Rendered",
        "P" => "Posted",
        "F" => "Failed",
        _ => status
    };
}

public sealed class DashboardSummary
{
    public int ClaimsToReview { get; set; }
    public int ClaimsWithWarnings { get; set; }
    public int ActiveAgreements { get; set; }
    public decimal AccruedThisYear { get; set; }
    public decimal OpenAccruals { get; set; }
    public int StatementsInProgress { get; set; }
    public int StatementsFailed { get; set; }
}

public sealed class TierProgress
{
    public int AgreementId { get; set; }
    public string AgreementNo { get; set; } = "";
    public string DistributorName { get; set; } = "";
    public string Basis { get; set; } = "";
    public int Period { get; set; }
    public decimal YtdQuantity { get; set; }
    public decimal YtdNetAmount { get; set; }
    public decimal RatePercent { get; set; }
    public decimal? ToNextTier { get; set; }
    public decimal? NextRate { get; set; }
}

public sealed class DistributorRate
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal NetAmount { get; set; }
    public decimal Accrued { get; set; }
    public decimal? AverageRate { get; set; }
}

public sealed class Dashboard
{
    public DashboardSummary Summary { get; set; } = new();
    public IReadOnlyList<TierProgress> NearNextTier { get; set; } = [];
    public IReadOnlyList<DistributorRate> Rates { get; set; } = [];
}

public sealed class Distributor
{
    public int DistributorId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string ErpVendorNo { get; set; } = "";
    public string Currency { get; set; } = "";
    public string? StatementEmail { get; set; }
    public string StatementFolder { get; set; } = "";
    public string? BankAccountMasked { get; set; }
    public bool IsActive { get; set; }
    public string ModifiedBy { get; set; } = "";
    public DateTime ModifiedAt { get; set; }
}

public sealed class AgreementRow
{
    public int AgreementId { get; set; }
    public string AgreementNo { get; set; } = "";
    public string Description { get; set; } = "";
    public string DistributorCode { get; set; } = "";
    public string DistributorName { get; set; } = "";
    public string Basis { get; set; } = "";
    public string? Category { get; set; }
    public string TiersText { get; set; } = "";
    public string TierMode { get; set; } = "";
    public int TierCount { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public decimal? CapAmount { get; set; }
    public string Status { get; set; } = "";
    public decimal? Accrued { get; set; }
}

public sealed class AgreementDetail
{
    public int AgreementId { get; set; }
    public string AgreementNo { get; set; } = "";
    public int DistributorId { get; set; }
    public string DistributorName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Basis { get; set; } = "V";
    public string? Category { get; set; }
    public string TiersText { get; set; } = "";
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public decimal? CapAmount { get; set; }
    public string Status { get; set; } = "D";
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string ModifiedBy { get; set; } = "";
    public DateTime ModifiedAt { get; set; }
    public byte[] RowVer { get; set; } = [];
}

public sealed class AccrualRow
{
    public int Period { get; set; }
    public decimal Quantity { get; set; }
    public decimal NetAmount { get; set; }
    public decimal YtdQuantity { get; set; }
    public decimal YtdNetAmount { get; set; }
    public decimal RatePercent { get; set; }
    public decimal YtdRebate { get; set; }
    public decimal AccruedAmount { get; set; }
    public decimal? ToNextTier { get; set; }
    public int? StatementId { get; set; }
}

public sealed class AgreementInput
{
    public int? AgreementId { get; set; }
    public string AgreementNo { get; set; } = "";
    public int DistributorId { get; set; }
    public string Description { get; set; } = "";
    public string Basis { get; set; } = "V";
    public string? Category { get; set; }
    public string Tiers { get; set; } = "";
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public decimal? CapAmount { get; set; }
    public byte[]? RowVer { get; set; }
}

public sealed class ClaimRow
{
    public int ClaimId { get; set; }
    public string DistributorCode { get; set; } = "";
    public string DistributorName { get; set; } = "";
    public int Period { get; set; }
    public string Source { get; set; } = "";
    public string? SourceRef { get; set; }
    public string Status { get; set; } = "";
    public int LineCount { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal TotalAmount { get; set; }
    public int WarningCount { get; set; }
    public int RejectedCount { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string ReceivedBy { get; set; } = "";
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
}

public sealed class ClaimHeader
{
    public int ClaimId { get; set; }
    public int DistributorId { get; set; }
    public string DistributorCode { get; set; } = "";
    public string DistributorName { get; set; } = "";
    public int Period { get; set; }
    public string Source { get; set; } = "";
    public string? SourceRef { get; set; }
    public string Status { get; set; } = "";
    public int LineCount { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal TotalAmount { get; set; }
    public int WarningCount { get; set; }
    public int RejectedCount { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string ReceivedBy { get; set; } = "";
    public DateTime? ValidatedAt { get; set; }
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }
    public byte[] RowVer { get; set; } = [];
}

public sealed class ClaimLineRow
{
    public int LineNumber { get; set; }
    public string InvoiceNo { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public string CustomerNo { get; set; } = "";
    public string Gtin { get; set; } = "";
    public string? ProductDescription { get; set; }
    public decimal Quantity { get; set; }
    public decimal NetAmount { get; set; }
    public decimal? ListNetPrice { get; set; }
    public string LineStatus { get; set; } = "";
    public string? Issue { get; set; }
}

public sealed class ClaimIssueRow
{
    public int LineNumber { get; set; }
    public string Message { get; set; } = "";
}

public sealed class ClaimDetail
{
    public ClaimHeader Header { get; set; } = new();
    public IReadOnlyList<ClaimLineRow> Lines { get; set; } = [];
    public IReadOnlyList<ClaimIssueRow> Issues { get; set; } = [];
}

public sealed class ClaimLineInput
{
    public string InvoiceNo { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public string CustomerNo { get; set; } = "";
    public string Gtin { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal NetAmount { get; set; }
}

public sealed class WorkbookImportResult
{
    public int? ClaimId { get; set; }
    public string? DistributorCode { get; set; }
    public int? Period { get; set; }
    public int Lines { get; set; }
    public string? Message { get; set; }
}

public sealed class StatementRow
{
    public int StatementId { get; set; }
    public string Quarter { get; set; } = "";
    public string DistributorCode { get; set; } = "";
    public string DistributorName { get; set; } = "";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
    public string RequestedBy { get; set; } = "";
    public DateTime RequestedAt { get; set; }
    public string? FilePath { get; set; }
    public string? ErpDocumentNo { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class StatementHeader
{
    public int StatementId { get; set; }
    public string Quarter { get; set; } = "";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
    public string RequestedBy { get; set; } = "";
    public DateTime RequestedAt { get; set; }
    public string? FilePath { get; set; }
    public string? ErpDocumentNo { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int DistributorId { get; set; }
    public string DistributorCode { get; set; } = "";
    public string DistributorName { get; set; } = "";
    public string ErpVendorNo { get; set; } = "";
    public string Currency { get; set; } = "";
    public string StatementFolder { get; set; } = "";
    public string? StatementEmail { get; set; }
    public string? BankAccountMasked { get; set; }
}

public sealed class StatementLine
{
    public string AgreementNo { get; set; } = "";
    public string Description { get; set; } = "";
    public string Basis { get; set; } = "";
    public string TiersText { get; set; } = "";
    public int Period { get; set; }
    public decimal Quantity { get; set; }
    public decimal NetAmount { get; set; }
    public decimal YtdQuantity { get; set; }
    public decimal YtdNetAmount { get; set; }
    public decimal RatePercent { get; set; }
    public decimal YtdRebate { get; set; }
    public decimal AccruedAmount { get; set; }
    public decimal? ToNextTier { get; set; }
}

public sealed class StatementDocument
{
    public StatementHeader Header { get; set; } = new();
    public IReadOnlyList<StatementLine> Lines { get; set; } = [];
}

public sealed class RequestedStatement
{
    public int StatementId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
}

public sealed class AuditRow
{
    public long AuditId { get; set; }
    public DateTime At { get; set; }
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public string Entity { get; set; } = "";
    public string? EntityId { get; set; }
    public string? Detail { get; set; }
}

public sealed class ApiClientInfo
{
    public int ApiClientId { get; set; }
    public int DistributorId { get; set; }
    public string DistributorCode { get; set; } = "";
    public string DistributorName { get; set; } = "";
}

public sealed class DistributorAgreement
{
    public string AgreementNo { get; set; } = "";
    public string Description { get; set; } = "";
    public string Basis { get; set; } = "";
    public string? Category { get; set; }
    public string TiersText { get; set; } = "";
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public int? LastPeriod { get; set; }
    public decimal? YtdQuantity { get; set; }
    public decimal? YtdNetAmount { get; set; }
    public decimal? RatePercent { get; set; }
    public decimal? YtdRebate { get; set; }
    public decimal? ToNextTier { get; set; }
}

public sealed class DistributorClaim
{
    public int ClaimId { get; set; }
    public int Period { get; set; }
    public string Source { get; set; } = "";
    public string? SourceRef { get; set; }
    public string Status { get; set; } = "";
    public int LineCount { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal TotalAmount { get; set; }
    public int WarningCount { get; set; }
    public int RejectedCount { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
}

/// <summary>Business rule violation reported by the database (THROW 50000-50999).</summary>
public sealed class RebateRuleException(int number, string message) : Exception(message)
{
    public int Number { get; } = number;
}

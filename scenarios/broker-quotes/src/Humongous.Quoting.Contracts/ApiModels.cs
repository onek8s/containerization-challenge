using Humongous.Quoting.Domain;

namespace Humongous.Quoting.Contracts;

public sealed record QuoteRequest
{
    public DateOnly InceptionDate { get; init; }
    public decimal? CommissionRate { get; init; }
    public string? BrokerReference { get; init; }
    public RiskDetails Risk { get; init; } = new();
}

public sealed record QuoteCreated(Guid Id, string Reference, QuoteStatus Status);

public sealed record QuoteSummary
{
    public Guid Id { get; init; }
    public string Reference { get; init; } = "";
    public string AgencyCode { get; init; } = "";
    public string InsuredName { get; init; } = "";
    public string Postcode { get; init; } = "";
    public string TradeCode { get; init; } = "";
    public QuoteStatus Status { get; init; }
    public decimal? Total { get; init; }
    public DateOnly InceptionDate { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateOnly? ValidUntil { get; init; }
    public bool IsRenewal { get; init; }
}

public sealed record QuoteHistoryEntry(DateTime AtUtc, string Event, string? By, string? Detail);

public sealed record QuoteDetail
{
    public Guid Id { get; init; }
    public string Reference { get; init; } = "";
    public string AgencyCode { get; init; } = "";
    public string AgencyName { get; init; } = "";
    public string? BrokerReference { get; init; }
    public QuoteStatus Status { get; init; }
    public string ProductVersion { get; init; } = "";
    public DateOnly InceptionDate { get; init; }
    public DateOnly? ValidUntil { get; init; }
    public decimal CommissionRate { get; init; }
    public RiskDetails Risk { get; init; } = new();
    public PremiumBreakdown? Premium { get; init; }
    public IReadOnlyList<Referral> Referrals { get; init; } = [];
    public IReadOnlyList<string> Clauses { get; init; } = [];
    public string? RatingEngine { get; init; }
    public string? RatingTables { get; init; }
    public string? RatingError { get; init; }
    public string? UnderwriterNote { get; init; }
    public Guid? PolicyId { get; init; }
    public Guid? RenewalOfPolicyId { get; init; }
    public IReadOnlyList<DocumentInfo> Documents { get; init; } = [];
    public IReadOnlyList<QuoteHistoryEntry> History { get; init; } = [];
}

public sealed record RequoteRequest
{
    public DateOnly? InceptionDate { get; init; }
    public decimal? CommissionRate { get; init; }
    public RiskDetails Risk { get; init; } = new();
}

public sealed record BindRequest
{
    public string? InsuredEmail { get; init; }
    public bool SendDocumentsToInsured { get; init; }
}

public sealed record ReferralDecisionRequest
{
    public ReferralDecision Decision { get; init; }
    public Dictionary<string, decimal> Loadings { get; init; } = [];
    public string? Note { get; init; }
}

public sealed record ReferralQueueItem
{
    public Guid QuoteId { get; init; }
    public string Reference { get; init; } = "";
    public string AgencyCode { get; init; } = "";
    public string InsuredName { get; init; } = "";
    public string TradeCode { get; init; } = "";
    public decimal? Total { get; init; }
    public int ReferralCount { get; init; }
    public DateTime ReferredUtc { get; init; }
    public DateOnly InceptionDate { get; init; }
    public string? AssignedTo { get; init; }
}

public sealed record PolicySummary
{
    public Guid Id { get; init; }
    public string PolicyNumber { get; init; } = "";
    public string AgencyCode { get; init; } = "";
    public string InsuredName { get; init; } = "";
    public PolicyStatus Status { get; init; }
    public DateOnly InceptionDate { get; init; }
    public DateOnly ExpiryDate { get; init; }
    public decimal AnnualGrossPremium { get; init; }
}

public sealed record PolicyTransactionInfo
{
    public Guid Id { get; init; }
    public TransactionType Type { get; init; }
    public DateOnly EffectiveDate { get; init; }
    public decimal GrossPremium { get; init; }
    public decimal Ipt { get; init; }
    public decimal Commission { get; init; }
    public string? Reason { get; init; }
    public DateTime CreatedUtc { get; init; }
}

public sealed record PolicyDetail
{
    public Guid Id { get; init; }
    public string PolicyNumber { get; init; } = "";
    public string AgencyCode { get; init; } = "";
    public PolicyStatus Status { get; init; }
    public DateOnly InceptionDate { get; init; }
    public DateOnly ExpiryDate { get; init; }
    public string ProductVersion { get; init; } = "";
    public RiskDetails Risk { get; init; } = new();
    public PremiumBreakdown Premium { get; init; } = new();
    public IReadOnlyList<string> Clauses { get; init; } = [];
    public Guid QuoteId { get; init; }
    public Guid? RenewalQuoteId { get; init; }
    public IReadOnlyList<PolicyTransactionInfo> Transactions { get; init; } = [];
    public IReadOnlyList<DocumentInfo> Documents { get; init; } = [];
}

public sealed record AdjustmentRequest
{
    public DateOnly EffectiveDate { get; init; }
    public string Reason { get; init; } = "";
    public RiskDetails Risk { get; init; } = new();
}

public sealed record CancellationRequest
{
    public DateOnly EffectiveDate { get; init; }
    public string Reason { get; init; } = "";
}

public sealed record DocumentInfo(Guid Id, string DocumentType, string FileName, DateTime CreatedUtc);

public sealed record AgencyInfo
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string FcaNumber { get; init; } = "";
    public decimal CommissionRate { get; init; }
    public bool Active { get; init; }
    public string DocumentEmail { get; init; } = "";
}

public sealed record AgencyUpdate
{
    public decimal CommissionRate { get; init; }
    public bool Active { get; init; }
    public string DocumentEmail { get; init; } = "";
}

public sealed record Dashboard
{
    public IReadOnlyList<StatusCount> QuotesByStatus { get; init; } = [];
    public decimal BoundGrossPremiumMonthToDate { get; init; }
    public int PoliciesLive { get; init; }
    public int RenewalsDueIn60Days { get; init; }
    public IReadOnlyList<QuoteSummary> RecentQuotes { get; init; } = [];
}

public sealed record StatusCount(string Status, int Count);

public sealed record BordereauRunInfo
{
    public Guid Id { get; init; }
    public string Period { get; init; } = "";
    public DateTime CreatedUtc { get; init; }
    public int Rows { get; init; }
    public decimal GrossPremium { get; init; }
    public string FileName { get; init; } = "";
    public string Status { get; init; } = "";
    public string? Error { get; init; }
}

public sealed record CatalogueInfo
{
    public IReadOnlyList<TradeInfo> Trades { get; init; } = [];
    public IReadOnlyList<ProductVersionInfo> Products { get; init; } = [];
    public IReadOnlyList<decimal> PublicLiabilityLimits { get; init; } = [];
}

public sealed record TradeInfo(string Code, string Description, string Group, string Acceptance);

public sealed record ProductVersionInfo(string Version, DateOnly EffectiveFrom, DateOnly? EffectiveTo, decimal IptRate, decimal MaxCommission, decimal PolicyFee);

using Humongous.Quoting.Domain;

namespace Humongous.Quoting.Data.Entities;

public class Agency
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string FcaNumber { get; set; } = "";
    public decimal CommissionRate { get; set; }
    public bool Active { get; set; } = true;
    public string DocumentEmail { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
}

public class Quote
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = "";
    public string AgencyCode { get; set; } = "";
    public Agency? Agency { get; set; }
    public string? BrokerReference { get; set; }
    public string CreatedBy { get; set; } = "";
    public QuoteStatus Status { get; set; }
    public string ProductVersion { get; set; } = "";
    public DateOnly InceptionDate { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public decimal CommissionRate { get; set; }

    public RiskDetails Risk { get; set; } = new();
    public PremiumBreakdown? Premium { get; set; }
    public Dictionary<string, decimal> Loadings { get; set; } = [];
    public List<string> Clauses { get; set; } = [];

    // Denormalised for lists, dashboards and the referral queue.
    public string InsuredName { get; set; } = "";
    public string Postcode { get; set; } = "";
    public string TradeCode { get; set; } = "";
    public decimal? TechnicalPremium { get; set; }
    public decimal? GrossPremium { get; set; }
    public decimal? Total { get; set; }

    public string? RatingEngine { get; set; }
    public string? RatingTables { get; set; }
    public string? RatingError { get; set; }
    public int RatingMilliseconds { get; set; }

    public DateTime? ReferredUtc { get; set; }
    public string? AssignedUnderwriter { get; set; }
    public string? DecidedBy { get; set; }
    public DateTime? DecidedUtc { get; set; }
    public string? UnderwriterNote { get; set; }

    public Guid? PolicyId { get; set; }
    public Guid? RenewalOfPolicyId { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public Guid ConcurrencyStamp { get; set; }

    public List<QuoteReferral> Referrals { get; set; } = [];
    public List<QuoteEvent> Events { get; set; } = [];

    public void SetRisk(RiskDetails risk)
    {
        Risk = risk;
        InsuredName = risk.Insured.Name;
        Postcode = Domain.Postcode.Normalise(risk.Insured.Postcode);
        TradeCode = risk.Insured.TradeCode;
    }

    public void Move(QuoteStatus to, string by, string? detail = null)
    {
        QuoteLifecycle.EnsureCanMove(Status, to);
        Status = to;
        Touch(to.ToString(), by, detail);
    }

    public void Touch(string evt, string by, string? detail = null)
    {
        UpdatedUtc = DateTime.UtcNow;
        ConcurrencyStamp = Guid.NewGuid();
        Events.Add(new QuoteEvent { QuoteId = Id, AtUtc = UpdatedUtc, Event = evt, By = by, Detail = detail });
    }
}

public class QuoteReferral
{
    public int Id { get; set; }
    public Guid QuoteId { get; set; }
    public string Code { get; set; } = "";
    public string Text { get; set; } = "";
    public string Source { get; set; } = "";
}

public class QuoteEvent
{
    public long Id { get; set; }
    public Guid QuoteId { get; set; }
    public DateTime AtUtc { get; set; }
    public string Event { get; set; } = "";
    public string? By { get; set; }
    public string? Detail { get; set; }
}

public class Policy
{
    public Guid Id { get; set; }
    public string PolicyNumber { get; set; } = "";
    public Guid QuoteId { get; set; }
    public string AgencyCode { get; set; } = "";
    public Agency? Agency { get; set; }
    public PolicyStatus Status { get; set; }
    public DateOnly InceptionDate { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public string ProductVersion { get; set; } = "";
    public RiskDetails Risk { get; set; } = new();
    public PremiumBreakdown Premium { get; set; } = new();
    public List<string> Clauses { get; set; } = [];
    public string InsuredName { get; set; } = "";
    public string Postcode { get; set; } = "";
    public string TradeCode { get; set; } = "";
    public decimal AnnualGrossPremium { get; set; }
    public decimal CommissionRate { get; set; }
    public Guid? PreviousPolicyId { get; set; }
    public Guid? RenewalQuoteId { get; set; }
    public DateTime? RenewalInvitedUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public Guid ConcurrencyStamp { get; set; }

    public List<PolicyTransaction> Transactions { get; set; } = [];
}

public class PolicyTransaction
{
    public Guid Id { get; set; }
    public Guid PolicyId { get; set; }
    public Policy? Policy { get; set; }
    public TransactionType Type { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public decimal GrossPremium { get; set; }
    public decimal Ipt { get; set; }
    public decimal Commission { get; set; }
    public decimal Fee { get; set; }
    public Dictionary<string, decimal> SectionGross { get; set; } = [];
    public string? Reason { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public Guid? BordereauRunId { get; set; }
}

public class StoredDocument
{
    public Guid Id { get; set; }
    public string DocumentType { get; set; } = "";
    public Guid? QuoteId { get; set; }
    public Guid? PolicyId { get; set; }
    public Guid? TransactionId { get; set; }
    public string AgencyCode { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/pdf";
    public byte[] Content { get; set; } = [];
    public string Sha256 { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public string? EmailedTo { get; set; }
    public DateTime? EmailedUtc { get; set; }
}

public class BordereauRun
{
    public Guid Id { get; set; }
    public string Period { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public int Rows { get; set; }
    public decimal GrossPremium { get; set; }
    public decimal Ipt { get; set; }
    public decimal Commission { get; set; }
    public string FileName { get; set; } = "";
    public byte[]? Content { get; set; }
    public string Status { get; set; } = "";
    public string? Error { get; set; }
    public DateTime? DeliveredUtc { get; set; }
}

public class NumberSequence
{
    public string Name { get; set; } = "";
    public long NextValue { get; set; }
}

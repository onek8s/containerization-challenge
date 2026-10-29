using Humongous.Quoting.Domain;

namespace Humongous.Quoting.Contracts.Messages;

// Messages on the Humongous bus. Exchange names follow the message type names.

public sealed record BindRequested
{
    public Guid QuoteId { get; init; }
    public string RequestedBy { get; init; } = "";
    public string? InsuredEmail { get; init; }
    public bool SendDocumentsToInsured { get; init; }
}

public sealed record PolicyIssued
{
    public Guid PolicyId { get; init; }
    public Guid QuoteId { get; init; }
    public string PolicyNumber { get; init; } = "";
    public string AgencyCode { get; init; } = "";
}

public sealed record BindFailed
{
    public Guid QuoteId { get; init; }
    public string AgencyCode { get; init; } = "";
    public string Reason { get; init; } = "";
}

public sealed record AdjustmentRequested
{
    public Guid PolicyId { get; init; }
    public DateOnly EffectiveDate { get; init; }
    public string Reason { get; init; } = "";
    public RiskDetails Risk { get; init; } = new();
    public string RequestedBy { get; init; } = "";
}

public sealed record AdjustmentProcessed
{
    public Guid PolicyId { get; init; }
    public Guid? TransactionId { get; init; }
    public string AgencyCode { get; init; } = "";
    public bool Succeeded { get; init; }
    public bool Referred { get; init; }
    public decimal AdditionalPremium { get; init; }
    public string? Message { get; init; }
}

public sealed record CancellationRequested
{
    public Guid PolicyId { get; init; }
    public DateOnly EffectiveDate { get; init; }
    public string Reason { get; init; } = "";
    public string RequestedBy { get; init; } = "";
}

public sealed record PolicyCancelled
{
    public Guid PolicyId { get; init; }
    public Guid TransactionId { get; init; }
    public string AgencyCode { get; init; } = "";
    public decimal ReturnPremium { get; init; }
}

public enum DocumentType
{
    QuoteSchedule,
    PolicySchedule,
    ElCertificate,
    AdjustmentEndorsement,
    CancellationNotice,
    RenewalInvitation
}

public sealed record DocumentRequested
{
    public DocumentType DocumentType { get; init; }
    public Guid? QuoteId { get; init; }
    public Guid? PolicyId { get; init; }
    public Guid? TransactionId { get; init; }
    public bool EmailToBroker { get; init; } = true;
    public string? AdditionalRecipient { get; init; }
}

public sealed record DocumentReady
{
    public Guid DocumentId { get; init; }
    public DocumentType DocumentType { get; init; }
    public Guid? QuoteId { get; init; }
    public Guid? PolicyId { get; init; }
    public string AgencyCode { get; init; } = "";
    public string FileName { get; init; } = "";
}

public sealed record RenewalInvited
{
    public Guid PolicyId { get; init; }
    public Guid RenewalQuoteId { get; init; }
    public string AgencyCode { get; init; } = "";
    public QuoteStatus Status { get; init; }
}

public sealed record QuoteStatusChanged
{
    public Guid QuoteId { get; init; }
    public string Reference { get; init; } = "";
    public string AgencyCode { get; init; } = "";
    public QuoteStatus Status { get; init; }
    public decimal? Total { get; init; }
}

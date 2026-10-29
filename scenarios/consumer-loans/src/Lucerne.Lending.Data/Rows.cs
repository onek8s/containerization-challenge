using Lucerne.Lending.Domain;

namespace Lucerne.Lending.Data;

public sealed class ProductRow
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string CoreProductCode { get; set; } = "";
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int MinTermMonths { get; set; }
    public int MaxTermMonths { get; set; }
    public int ReferenceRate { get; set; }
    public decimal BaseMarginPercent { get; set; }
    public decimal ArrangementFee { get; set; }
    public decimal MonthlyFee { get; set; }
    public bool RequiresCollateral { get; set; }
    public bool AllowsCoApplicant { get; set; }
    public string Purposes { get; set; } = "";
    public bool Active { get; set; }

    public LoanProduct ToProduct() => new()
    {
        Code = Code,
        Name = Name,
        CoreProductCode = CoreProductCode.Trim(),
        MinAmount = MinAmount,
        MaxAmount = MaxAmount,
        MinTermMonths = MinTermMonths,
        MaxTermMonths = MaxTermMonths,
        ReferenceRate = (ReferenceRateType)ReferenceRate,
        BaseMarginPercent = BaseMarginPercent,
        ArrangementFee = ArrangementFee,
        MonthlyFee = MonthlyFee,
        RequiresCollateral = RequiresCollateral,
        AllowsCoApplicant = AllowsCoApplicant,
        Purposes = Purposes.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<LoanPurpose>).ToArray(),
        Active = Active
    };
}

public sealed class ApplicationRow
{
    public long Id { get; set; }
    public string Reference { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public int Purpose { get; set; }
    public decimal Amount { get; set; }
    public int TermMonths { get; set; }
    public int DueDay { get; set; }
    public int Channel { get; set; }
    public int Status { get; set; }
    public string? CollateralDescription { get; set; }
    public decimal? CollateralValue { get; set; }
    public string? DisbursementIban { get; set; }
    public bool ConsentBureau { get; set; }
    public bool ConsentMarketing { get; set; }
    public string? CustomerUserId { get; set; }
    public string? AssignedTo { get; set; }
    public string? CoreAccountNumber { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? SubmittedUtc { get; set; }
    public DateTime? DecidedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public int Version { get; set; }

    public ApplicationStatus StatusValue => (ApplicationStatus)Status;
}

/// <summary>Application with its applicants as read for processing.</summary>
public sealed class ApplicationRecord
{
    public ApplicationRecord(ApplicationRow row, List<Applicant> applicants)
    {
        Row = row;
        Applicants = applicants;
    }

    public ApplicationRow Row { get; }
    public List<Applicant> Applicants { get; }

    public LoanApplication ToDomain() => new()
    {
        Id = Row.Id,
        Reference = Row.Reference,
        ProductCode = Row.ProductCode,
        Purpose = (LoanPurpose)Row.Purpose,
        Amount = Row.Amount,
        TermMonths = Row.TermMonths,
        DueDay = Row.DueDay,
        Channel = (Channel)Row.Channel,
        Status = (ApplicationStatus)Row.Status,
        CollateralDescription = Row.CollateralDescription,
        CollateralValue = Row.CollateralValue,
        DisbursementIban = Row.DisbursementIban,
        ConsentBureau = Row.ConsentBureau,
        ConsentMarketing = Row.ConsentMarketing,
        CustomerUserId = Row.CustomerUserId,
        AssignedTo = Row.AssignedTo,
        CreatedUtc = Row.CreatedUtc,
        SubmittedUtc = Row.SubmittedUtc,
        DecidedUtc = Row.DecidedUtc,
        UpdatedUtc = Row.UpdatedUtc,
        Applicants = Applicants
    };
}

public sealed class QueueItem
{
    public long Id { get; set; }
    public string Reference { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public decimal Amount { get; set; }
    public int TermMonths { get; set; }
    public int Status { get; set; }
    public string ApplicantName { get; set; } = "";
    public string PersonalIdentityCode { get; set; } = "";
    public string? AssignedTo { get; set; }
    public string? Outcome { get; set; }
    public string? RiskGrade { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public int Channel { get; set; }
}

public sealed class BureauReportRow
{
    public long Id { get; set; }
    public long ApplicantId { get; set; }
    public string Provider { get; set; } = "";
    public string RequestId { get; set; } = "";
    public int? Score { get; set; }
    public bool PaymentDefault { get; set; }
    public int DefaultCount { get; set; }
    public bool DebtRestructuring { get; set; }
    public bool CreditBlock { get; set; }
    public int ReportedLoans { get; set; }
    public string RawXml { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
}

public sealed class DecisionRow
{
    public long Id { get; set; }
    public long ApplicationId { get; set; }
    public string Outcome { get; set; } = "";
    public string RiskGrade { get; set; } = "X";
    public int Score { get; set; }
    public string ReasonCodes { get; set; } = "";
    public string ScorecardJson { get; set; } = "[]";
    public string RuleTraceJson { get; set; } = "[]";
    public string AffordabilityJson { get; set; } = "{}";
    public string PolicyVersion { get; set; } = "";
    public string DecidedBy { get; set; } = "";
    public bool IsOverride { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class OfferRow
{
    public long Id { get; set; }
    public long ApplicationId { get; set; }
    public decimal Amount { get; set; }
    public int TermMonths { get; set; }
    public string ReferenceRateCode { get; set; } = "FIX";
    public decimal ReferenceRatePercent { get; set; }
    public decimal MarginPercent { get; set; }
    public decimal NominalRatePercent { get; set; }
    public decimal AprPercent { get; set; }
    public decimal MonthlyPayment { get; set; }
    public decimal MonthlyFee { get; set; }
    public decimal ArrangementFee { get; set; }
    public decimal TotalRepayable { get; set; }
    public DateTime ValidUntilUtc { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime CreatedUtc { get; set; }
    public DateTime? AcceptedUtc { get; set; }
}

public sealed class DocumentRow
{
    public long Id { get; set; }
    public long? ApplicationId { get; set; }
    public long? CollectionCaseId { get; set; }
    public string Kind { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/pdf";
    public byte[]? Content { get; set; }
    public string? Sha256 { get; set; }
    public string Status { get; set; } = "Pending";
    public string? SigningOrderId { get; set; }
    public string? SigningUrl { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? SignedUtc { get; set; }
}

public static class DocumentKinds
{
    public const string PreContractInformation = "SECCI";
    public const string LoanAgreement = "AGREEMENT";
    public const string SignedAgreement = "AGREEMENT_SIGNED";
    public const string DeclineLetter = "DECLINE";
    public const string CollectionLetter = "COLLECTION";
    public const string PaymentPlanConfirmation = "PLAN";
}

public sealed class NoteRow
{
    public long Id { get; set; }
    public long ApplicationId { get; set; }
    public string Author { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
}

public sealed class AuditRow
{
    public long Id { get; set; }
    public long? ApplicationId { get; set; }
    public string EventType { get; set; } = "";
    public string Actor { get; set; } = "";
    public string? Detail { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class OutboxMessage
{
    public long Id { get; set; }
    public string Topic { get; set; } = "";
    public string Payload { get; set; } = "";
    public int Attempts { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public static class Topics
{
    public const string DocumentRequested = "document.requested";
    public const string SigningRequested = "signing.requested";
    public const string LoanBooked = "loan.booked";
    public const string CustomerNotification = "customer.notification";
    public const string SigningCallback = "signing.callback";
}

public sealed class LoanRow
{
    public string AccountNumber { get; set; } = "";
    public long? ApplicationId { get; set; }
    public string CustomerNumber { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal NominalRatePercent { get; set; }
    public decimal MonthlyInstalment { get; set; }
    public DateTime BookedUtc { get; set; }
    public DateTime? DisbursedUtc { get; set; }
    public decimal Balance { get; set; }
    public decimal Arrears { get; set; }
    public int DaysPastDue { get; set; }
    public string CoreStatus { get; set; } = "";
    public DateTime? LastEventUtc { get; set; }
}

public sealed class CollectionCaseRow
{
    public long Id { get; set; }
    public string Reference { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string CustomerNumber { get; set; } = "";
    public int Stage { get; set; }
    public int Status { get; set; }
    public decimal Arrears { get; set; }
    public int DaysPastDue { get; set; }
    public int FeesCharged { get; set; }
    public string? AssignedTo { get; set; }
    public DateOnly? PromiseDate { get; set; }
    public decimal? PromiseAmount { get; set; }
    public DateTime OpenedUtc { get; set; }
    public DateTime? LastActionUtc { get; set; }
    public DateTime? ClosedUtc { get; set; }
    public int Version { get; set; }

    public CollectionStage StageValue => (CollectionStage)Stage;
    public CollectionStatus StatusValue => (CollectionStatus)Status;
}

public sealed class CollectionActionRow
{
    public long Id { get; set; }
    public long CaseId { get; set; }
    public string ActionType { get; set; } = "";
    public string? Detail { get; set; }
    public string Actor { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
}

public sealed class PaymentPlanRow
{
    public long Id { get; set; }
    public long CaseId { get; set; }
    public string AccountNumber { get; set; } = "";
    public int Months { get; set; }
    public string InstalmentsJson { get; set; } = "[]";
    public string Status { get; set; } = "Active";
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
}

public sealed class GlEntryRow
{
    public long Id { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string GlAccount { get; set; } = "";
    public string CostCenter { get; set; } = "";
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string Text { get; set; } = "";
    public string SourceType { get; set; } = "";
    public string SourceRef { get; set; } = "";
    public string? ExportBatch { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class JobRunRow
{
    public long Id { get; set; }
    public string Job { get; set; } = "";
    public string Host { get; set; } = "";
    public DateTime StartedUtc { get; set; }
    public DateTime? FinishedUtc { get; set; }
    public string Status { get; set; } = "";
    public string? Detail { get; set; }
}

public sealed class ApiClientRow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string KeyHash { get; set; } = "";
    public string Scopes { get; set; } = "";
    public bool Active { get; set; }
}

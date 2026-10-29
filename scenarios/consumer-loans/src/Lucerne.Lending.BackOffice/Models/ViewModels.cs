using Lucerne.Lending.CoreBanking;
using Lucerne.Lending.Data;
using Lucerne.Lending.Domain;

namespace Lucerne.Lending.BackOffice.Models;

public sealed class DashboardModel
{
    public IReadOnlyDictionary<ApplicationStatus, int> ByStatus { get; init; } = new Dictionary<ApplicationStatus, int>();
    public IReadOnlyList<QueueItem> Mine { get; init; } = Array.Empty<QueueItem>();
    public int OpenCollectionCases { get; init; }
    public int PendingDocuments { get; init; }
    public IReadOnlyList<JobRunRow> Jobs { get; init; } = Array.Empty<JobRunRow>();
}

public sealed class QueueModel
{
    public string Queue { get; init; } = "referred";
    public string? Search { get; init; }
    public int Page { get; init; }
    public IReadOnlyList<QueueItem> Items { get; init; } = Array.Empty<QueueItem>();
}

public sealed class CustomerView
{
    public Applicant Applicant { get; init; } = new();
    public CoreCustomer? Core { get; init; }
    public CoreExposure? Exposure { get; init; }
    public IReadOnlyList<CoreAccount> Accounts { get; init; } = Array.Empty<CoreAccount>();
    public string? CoreError { get; init; }
}

public sealed class ApplicationDetailsModel
{
    public ApplicationRecord Application { get; init; } = null!;
    public LoanProduct Product { get; init; } = null!;
    public IReadOnlyList<CustomerView> Customers { get; init; } = Array.Empty<CustomerView>();
    public IReadOnlyList<DecisionRow> Decisions { get; init; } = Array.Empty<DecisionRow>();
    public IReadOnlyList<BureauReportRow> BureauReports { get; init; } = Array.Empty<BureauReportRow>();
    public OfferRow? Offer { get; init; }
    public IReadOnlyList<DocumentRow> Documents { get; init; } = Array.Empty<DocumentRow>();
    public IReadOnlyList<NoteRow> Notes { get; init; } = Array.Empty<NoteRow>();
    public IReadOnlyList<AuditRow> Audit { get; init; } = Array.Empty<AuditRow>();
    public bool CanDecide { get; init; }
    public bool CanBook { get; init; }
}

public sealed class DecideForm
{
    public string Reference { get; set; } = "";
    public int Version { get; set; }
    public bool Approve { get; set; }
    public RiskGrade? Grade { get; set; }
    public decimal? Amount { get; set; }
    public int? TermMonths { get; set; }
    public decimal? MarginOverridePercent { get; set; }
    public string Comment { get; set; } = "";
}

public sealed class CollectionDetailsModel
{
    public CollectionCaseRow Case { get; init; } = null!;
    public CoreLoanSummary? Loan { get; init; }
    public CoreCustomer? Customer { get; init; }
    public IReadOnlyList<CorePayment> Payments { get; init; } = Array.Empty<CorePayment>();
    public IReadOnlyList<CollectionActionRow> Actions { get; init; } = Array.Empty<CollectionActionRow>();
    public IReadOnlyList<PaymentPlanRow> Plans { get; init; } = Array.Empty<PaymentPlanRow>();
    public IReadOnlyList<DocumentRow> Documents { get; init; } = Array.Empty<DocumentRow>();
    public string? CoreError { get; init; }
}

public sealed class CustomerSearchModel
{
    public string? Pic { get; init; }
    public CoreCustomer? Customer { get; init; }
    public CoreExposure? Exposure { get; init; }
    public IReadOnlyList<CoreAccount> Accounts { get; init; } = Array.Empty<CoreAccount>();
    public IReadOnlyList<ApplicationRow> Applications { get; init; } = Array.Empty<ApplicationRow>();
    public string? Message { get; init; }
}

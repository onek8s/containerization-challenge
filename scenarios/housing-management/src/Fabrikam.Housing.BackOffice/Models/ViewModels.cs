using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Fabrikam.Housing.Assets;
using Fabrikam.Housing.Compliance;
using Fabrikam.Housing.Contractors;
using Fabrikam.Housing.Data.Queries;
using Fabrikam.Housing.Documents;
using Fabrikam.Housing.Integration.DocumentStore;
using Fabrikam.Housing.Rent;
using Fabrikam.Housing.Repairs;
using Fabrikam.Housing.Tenancies;

namespace Fabrikam.Housing.BackOffice.Models
{
    public class DashboardViewModel
    {
        public string Patch { get; set; }
        public IDictionary<string, int> Counts { get; set; }
        public IList<ArrearsRow> TopArrears { get; set; }
        public IList<TenantEnquiry> OpenEnquiries { get; set; }
        public IList<WorkOrderListItem> Emergencies { get; set; }
    }

    public class PropertySearchViewModel
    {
        public string Q { get; set; }
        public string Estate { get; set; }
        public string Patch { get; set; }
        public PropertyStatus? Status { get; set; }
        public int Page { get; set; } = 1;
        public int Total { get; set; }
        public IList<Estate> Estates { get; set; }
        public IList<PropertyListItem> Results { get; set; } = new List<PropertyListItem>();
    }

    public class PropertyListItem
    {
        public int Id { get; set; }
        public string Reference { get; set; }
        public string AddressLine1 { get; set; }
        public string Postcode { get; set; }
        public string EstateName { get; set; }
        public string PatchCode { get; set; }
        public PropertyType Type { get; set; }
        public int Bedrooms { get; set; }
        public PropertyStatus Status { get; set; }
        public decimal WeeklyTotalCharge { get; set; }
    }

    public class ComplianceLine
    {
        public ComplianceType Type { get; set; }
        public ComplianceStatus Status { get; set; }
        public ComplianceCertificate Latest { get; set; }
    }

    public class PropertyDetailViewModel
    {
        public Property Property { get; set; }
        public Tenancy Tenancy { get; set; }
        public decimal? Balance { get; set; }
        public IList<ComplianceLine> Compliance { get; set; }
        public IList<WorkOrderListItem> Repairs { get; set; }
        public IList<SearchHit> Documents { get; set; }
        public string DocumentError { get; set; }
    }

    public class WorkOrderListItem
    {
        public int Id { get; set; }
        public string Reference { get; set; }
        public string PropertyAddress { get; set; }
        public string PatchCode { get; set; }
        public string Description { get; set; }
        public Trade Trade { get; set; }
        public RepairPriority Priority { get; set; }
        public WorkOrderStatus Status { get; set; }
        public DateTime ReportedAt { get; set; }
        public DateTime TargetDate { get; set; }
        public string ContractorCode { get; set; }
        public bool Overdue { get; set; }
    }

    public class TenancyDetailViewModel
    {
        public Tenancy Tenancy { get; set; }
        public decimal Balance { get; set; }
        public IList<StatementLine> RecentLines { get; set; }
        public IList<DirectDebitMandate> Mandates { get; set; }
        public IList<PaymentArrangement> Arrangements { get; set; }
        public ArrearsCase ArrearsCase { get; set; }
        public ArrearsDecision Decision { get; set; }
        public IList<TenantEnquiry> Enquiries { get; set; }
        public IList<Communications.CommunicationLog> Communications { get; set; }
        public AdjustmentInput Adjustment { get; set; } = new AdjustmentInput();
        public ArrangementInput Arrangement { get; set; } = new ArrangementInput();
    }

    public class AdjustmentInput
    {
        [Required]
        public TransactionType Type { get; set; } = TransactionType.Adjustment;
        [Required, Range(-10000, 10000)]
        public decimal Amount { get; set; }
        [Required, StringLength(200)]
        public string Description { get; set; }
    }

    public class ArrangementInput
    {
        [Range(1, 500)]
        public decimal WeeklyAmount { get; set; }
        public DateTime StartDate { get; set; } = DateTime.Today;
        [StringLength(500)]
        public string Notes { get; set; }
    }

    public class StatementViewModel
    {
        public Tenancy Tenancy { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public IList<StatementLine> Lines { get; set; }
    }

    public class RaiseWorkOrderInput
    {
        public int PropertyId { get; set; }
        public string PropertyAddress { get; set; }
        [Required, StringLength(100)]
        public string Location { get; set; }
        [Required, StringLength(2000)]
        public string Description { get; set; }
        [Required]
        public string SorCode { get; set; }
        [Range(0.1, 999)]
        public decimal Quantity { get; set; } = 1;
        public RepairPriority? Priority { get; set; }
        [StringLength(100)]
        public string ReportedBy { get; set; }
        public IList<ScheduleOfRatesItem> Sor { get; set; }
    }

    public class WorkOrderDetailViewModel
    {
        public WorkOrder Order { get; set; }
        public Tenancy Tenancy { get; set; }
        public IList<Contractor> Contractors { get; set; }
        public Contractor Suggested { get; set; }
        public IList<WorkOrderStatus> NextStatuses { get; set; }
        public IList<DateTime> AppointmentDays { get; set; }
        public IList<ScheduleOfRatesItem> Sor { get; set; }
        public bool Overdue { get; set; }
    }

    public class ContractorEditViewModel
    {
        public Contractor Contractor { get; set; }
        public IList<string> AllTrades { get; set; }
        public IList<string> SelectedTrades { get; set; } = new List<string>();
        public string NewSecret { get; set; }
        public int OpenJobs { get; set; }
    }

    public class InvoiceReviewItem
    {
        public ContractorInvoice Invoice { get; set; }
        public string MatchProblem { get; set; }
        public decimal PricedWork { get; set; }
    }

    public class ComplianceDashboardViewModel
    {
        public IDictionary<ComplianceType, IDictionary<ComplianceStatus, int>> Counts { get; set; }
        public IList<ComplianceDueItem> Due { get; set; }
        public int DaysAhead { get; set; }
        public ComplianceType? Type { get; set; }
    }

    public class ComplianceDueItem
    {
        public int PropertyId { get; set; }
        public string PropertyReference { get; set; }
        public string Address { get; set; }
        public string PatchCode { get; set; }
        public ComplianceType Type { get; set; }
        public DateTime? ExpiresOn { get; set; }
        public ComplianceStatus Status { get; set; }
    }

    public class CertificateUploadInput
    {
        public int PropertyId { get; set; }
        public string PropertyAddress { get; set; }
        [Required]
        public ComplianceType Type { get; set; }
        [Required, StringLength(40)]
        public string CertificateNumber { get; set; }
        [Required]
        public DateTime InspectedOn { get; set; } = DateTime.Today;
        [Required]
        public CertificateOutcome Outcome { get; set; } = CertificateOutcome.Satisfactory;
        [Required, StringLength(100)]
        public string EngineerName { get; set; }
        [StringLength(20)]
        public string EngineerRegistration { get; set; }
        public int? ContractorId { get; set; }
        [StringLength(1000)]
        public string Observations { get; set; }
        public IList<Contractor> Contractors { get; set; }
    }

    public class DocumentSearchViewModel
    {
        public string Q { get; set; }
        public int? TenancyId { get; set; }
        public int? PropertyId { get; set; }
        public IList<SearchHit> Hits { get; set; } = new List<SearchHit>();
        public string Error { get; set; }
    }

    public class DocumentUploadInput
    {
        public int? PropertyId { get; set; }
        public int? TenancyId { get; set; }
        [Required]
        public DocumentCategory Category { get; set; } = DocumentCategory.Correspondence;
        [Required, StringLength(200)]
        public string Title { get; set; }
        public bool VisibleToTenant { get; set; }
    }

    public class RentReviewViewModel
    {
        public decimal CpiPercent { get; set; } = 0.5m;
        public int Year { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public IList<RentReviewLine> Lines { get; set; } = new List<RentReviewLine>();
        public decimal CurrentTotal { get; set; }
        public decimal NewTotal { get; set; }
    }

    public class RentReviewLine
    {
        public int PropertyId { get; set; }
        public string Reference { get; set; }
        public string Address { get; set; }
        public int Bedrooms { get; set; }
        public decimal CurrentRent { get; set; }
        public decimal NewRent { get; set; }
        public bool Capped { get; set; }
    }

    public class ErrorViewModel
    {
        public string RequestId { get; set; }
    }
}

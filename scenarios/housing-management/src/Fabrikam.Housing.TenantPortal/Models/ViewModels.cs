using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Fabrikam.Housing.Data.Queries;
using Fabrikam.Housing.Rent;
using Fabrikam.Housing.Repairs;
using Fabrikam.Housing.Tenancies;

namespace Fabrikam.Housing.TenantPortal.Models
{
    public class DashboardViewModel
    {
        public string FirstName { get; set; }
        public string TenancyReference { get; set; }
        public string Address { get; set; }
        public decimal Balance { get; set; }
        public decimal WeeklyCharge { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public decimal? LastPaymentAmount { get; set; }
        public MandateViewModel DirectDebit { get; set; }
        public IList<RepairSummary> OpenRepairs { get; set; } = new List<RepairSummary>();
        public DateTime? GasCheckDue { get; set; }
        public int NewLetters { get; set; }
        public int UnansweredMessages { get; set; }
    }

    public class MandateViewModel
    {
        public int Id { get; set; }
        public MandateStatus Status { get; set; }
        public CollectionFrequency Frequency { get; set; }
        public int CollectionDay { get; set; }
        public decimal Amount { get; set; }
        public DateTime? NextCollectionDate { get; set; }
        public string AccountHolderName { get; set; }
        public string AccountNumberEnding { get; set; }

        public string Schedule => Frequency == CollectionFrequency.Monthly
            ? $"Monthly on day {CollectionDay}"
            : $"{Frequency} on {(DayOfWeek)(CollectionDay % 7)}";
    }

    public class StatementViewModel
    {
        [DataType(DataType.Date)] public DateTime From { get; set; }
        [DataType(DataType.Date)] public DateTime To { get; set; }
        public string TenancyReference { get; set; }
        public IList<StatementLine> Lines { get; set; } = new List<StatementLine>();
        public decimal Balance { get; set; }
    }

    public class PayViewModel
    {
        public decimal Balance { get; set; }
        public decimal WeeklyCharge { get; set; }

        [Required, Range(1.00, 5000.00, ErrorMessage = "Enter an amount between £1 and £5,000.")]
        public decimal? Amount { get; set; }
    }

    public class PayResultViewModel
    {
        public bool Succeeded { get; set; }
        public decimal Amount { get; set; }
        public string PaymentReference { get; set; }
        public string Status { get; set; }
    }

    public class DirectDebitViewModel
    {
        public MandateViewModel Current { get; set; }
        public decimal WeeklyCharge { get; set; }
        public decimal Balance { get; set; }

        [Required] public CollectionFrequency Frequency { get; set; } = CollectionFrequency.Monthly;
        [Required, Range(1, 28)] public int CollectionDay { get; set; } = 1;
        [Required, Range(1.00, 3000.00)] public decimal? Amount { get; set; }
        public decimal SuggestedWeekly { get; set; }
        public decimal SuggestedFortnightly { get; set; }
        public decimal SuggestedFourWeekly { get; set; }
        public decimal SuggestedMonthly { get; set; }

        [Range(typeof(bool), "true", "true", ErrorMessage = "You must confirm you are the account holder and can authorise debits on the account.")]
        public bool ConfirmAccountHolder { get; set; }
    }

    public class RepairSummary
    {
        public int Id { get; set; }
        public string Reference { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public RepairPriority Priority { get; set; }
        public WorkOrderStatus Status { get; set; }
        public DateTime ReportedAt { get; set; }
        public DateTime TargetDate { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? NextAppointment { get; set; }
        public AppointmentSlot? NextSlot { get; set; }
        public string ContractorName { get; set; }

        public string StatusText
        {
            get
            {
                switch (Status)
                {
                    case WorkOrderStatus.Reported:
                    case WorkOrderStatus.Triaged: return "We are reviewing your repair";
                    case WorkOrderStatus.Assigned:
                    case WorkOrderStatus.Accepted: return "Passed to our contractor";
                    case WorkOrderStatus.Appointed: return "Appointment booked";
                    case WorkOrderStatus.InProgress: return "Work in progress";
                    case WorkOrderStatus.NoAccess: return "We could not get in - please rebook";
                    case WorkOrderStatus.OnHold: return "On hold - we will contact you";
                    case WorkOrderStatus.Completed: return "Completed";
                    default: return "Cancelled";
                }
            }
        }
    }

    public class RepairDetailViewModel : RepairSummary
    {
        public IList<Appointment> Appointments { get; set; } = new List<Appointment>();
        public IList<WorkOrderEvent> Events { get; set; } = new List<WorkOrderEvent>();
        public IList<DocumentViewModel> Photos { get; set; } = new List<DocumentViewModel>();
        public int? SatisfactionScore { get; set; }
        public bool CanCancel { get; set; }
        public bool CanRate { get; set; }
    }

    public class DocumentViewModel
    {
        public int Id { get; set; }
        public Guid DocumentKey { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public long SizeBytes { get; set; }
        public DateTime UploadedAt { get; set; }
    }

    public class EnquiryViewModel
    {
        public int Id { get; set; }
        public EnquiryTopic Topic { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Response { get; set; }
        public DateTime? RespondedAt { get; set; }
    }

    public class NewEnquiryViewModel
    {
        [Required] public EnquiryTopic? Topic { get; set; }
        [Required, StringLength(200)] public string Subject { get; set; }
        [Required, StringLength(4000, MinimumLength = 10)] public string Message { get; set; }
    }

    public class ProfileViewModel
    {
        public string FullName { get; set; }
        public string TenancyReference { get; set; }
        public string Address { get; set; }

        [EmailAddress, StringLength(200), Display(Name = "E-mail address")]
        public string Email { get; set; }

        [RegularExpression(@"^\s*(\+44\s?7\d{3}|07\d{3})\s?\d{3}\s?\d{3}\s*$", ErrorMessage = "Enter a UK mobile number, for example 07700 900123.")]
        [Display(Name = "Mobile phone")]
        public string MobilePhone { get; set; }

        [Display(Name = "How should we contact you?")]
        public ContactPreference ContactPreference { get; set; }

        [Display(Name = "Send me letters in large print")]
        public bool NeedsLargePrint { get; set; }
    }

    public class ErrorViewModel
    {
        public string RequestId { get; set; }
    }
}

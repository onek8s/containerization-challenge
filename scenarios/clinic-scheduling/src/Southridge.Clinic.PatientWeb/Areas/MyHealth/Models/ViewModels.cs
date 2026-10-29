using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Southridge.Clinic.Data;
using Southridge.Clinic.PatientWeb.BookingWs;

namespace Southridge.Clinic.PatientWeb.Areas.MyHealth.Models
{
    public class AppointmentsViewModel
    {
        public string PatientName { get; set; }
        public IList<AppointmentItem> Upcoming { get; set; }
        public IList<AppointmentItem> Past { get; set; }
    }

    public class CancelViewModel
    {
        public AppointmentItem Appointment { get; set; }

        [StringLength(200)]
        [Display(Name = "Reason (optional)")]
        public string Reason { get; set; }
    }

    public class StatementsViewModel
    {
        public decimal Balance { get; set; }
        public IList<StatementRow> Statements { get; set; }
        public IList<PatientClaimRow> Claims { get; set; }
    }

    public class IntakeFormModel
    {
        public int? AppointmentId { get; set; }

        public string AppointmentText { get; set; }

        [Required(ErrorMessage = "Tell us why you are coming in.")]
        [StringLength(1000)]
        [Display(Name = "Reason for your visit")]
        [DataType(DataType.MultilineText)]
        public string ReasonForVisit { get; set; }

        [StringLength(2000)]
        [Display(Name = "Medications you take (name, dose, how often)")]
        [DataType(DataType.MultilineText)]
        public string Medications { get; set; }

        [StringLength(1000)]
        [Display(Name = "Allergies")]
        [DataType(DataType.MultilineText)]
        public string Allergies { get; set; }

        [StringLength(2000)]
        [Display(Name = "Ongoing conditions")]
        [DataType(DataType.MultilineText)]
        public string Conditions { get; set; }

        [StringLength(100)]
        [Display(Name = "Pharmacy")]
        public string PharmacyName { get; set; }

        [RegularExpression(@"^\(?\d{3}\)?[ .-]?\d{3}[ .-]?\d{4}$", ErrorMessage = "Enter a 10 digit phone number.")]
        [Display(Name = "Pharmacy phone")]
        public string PharmacyPhone { get; set; }

        public DateTime? SubmittedOn { get; set; }
    }
}

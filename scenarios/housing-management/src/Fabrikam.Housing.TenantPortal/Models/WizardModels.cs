using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Fabrikam.Housing.Repairs;
using Fabrikam.Housing.TenantPortal.Services;

namespace Fabrikam.Housing.TenantPortal.Models
{
    public class AreaStepViewModel
    {
        public IReadOnlyList<string> Areas { get; set; }
        [Required(ErrorMessage = "Choose where the problem is.")] public string Area { get; set; }
    }

    public class ProblemStepViewModel
    {
        public string Area { get; set; }
        public IList<RepairFinder.Option> Options { get; set; } = new List<RepairFinder.Option>();
        [Required(ErrorMessage = "Choose the problem that best matches.")] public string OptionKey { get; set; }
    }

    public class DetailsStepViewModel
    {
        public RepairFinder.Option Option { get; set; }
        public RepairPriority Priority { get; set; }

        [Required, StringLength(100), Display(Name = "Which room or where exactly?")]
        public string Location { get; set; }

        [Required, StringLength(2000, MinimumLength = 10), Display(Name = "Tell us more about the problem")]
        public string Description { get; set; }
    }

    public class PhotosStepViewModel
    {
        public bool PhotoSuggested { get; set; }
        public IList<DraftPhoto> Photos { get; set; } = new List<DraftPhoto>();
        public int MaxPhotos { get; set; }
    }

    public class AppointmentStepViewModel
    {
        public RepairPriority Priority { get; set; }
        public IList<DateTime> Days { get; set; } = new List<DateTime>();
        [Required(ErrorMessage = "Choose a day.")] public DateTime? Date { get; set; }
        [Required(ErrorMessage = "Choose a time slot.")] public AppointmentSlot? Slot { get; set; }
    }

    public class ConfirmStepViewModel
    {
        public RepairDraft Draft { get; set; }
        public RepairFinder.Option Option { get; set; }
        public RepairPriority Priority { get; set; }
        public DateTime TargetDate { get; set; }
        public string Address { get; set; }
    }

    public class RepairReportedViewModel
    {
        public string Reference { get; set; }
        public RepairPriority Priority { get; set; }
        public DateTime TargetDate { get; set; }
        public DateTime? AppointmentDate { get; set; }
        public AppointmentSlot? Slot { get; set; }
        public int PhotosUploaded { get; set; }
        public int PhotosFailed { get; set; }
    }

    public static class SlotText
    {
        public static string For(AppointmentSlot? slot)
        {
            switch (slot)
            {
                case AppointmentSlot.Morning: return "8am to 12pm";
                case AppointmentSlot.Afternoon: return "12pm to 5pm";
                case AppointmentSlot.SchoolRun: return "9:30am to 2:30pm";
                case AppointmentSlot.AllDay: return "8am to 5pm";
                default: return "";
            }
        }
    }
}

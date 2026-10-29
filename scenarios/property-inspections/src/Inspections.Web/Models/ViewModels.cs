using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Northwind.Inspections.Contracts;
using Northwind.Inspections.Core;

namespace Northwind.Inspections.Web.Models
{
    public class LoginModel
    {
        [Required, StringLength(64)]
        [Display(Name = "User name")]
        public string Login { get; set; }

        [Required, DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Keep me signed in")]
        public bool Remember { get; set; }

        public string ReturnUrl { get; set; }
    }

    [Serializable]
    public class InspectionForm
    {
        public int Id { get; set; }

        [Required, StringLength(20)]
        [Display(Name = "Claim number")]
        public string ClaimNumber { get; set; }

        [Required, StringLength(200)]
        [Display(Name = "Policy holder")]
        public string PolicyHolder { get; set; }

        [Required, StringLength(400)]
        [Display(Name = "Property address")]
        public string PropertyAddress { get; set; }

        [Required]
        [Display(Name = "Peril")]
        public string PerilCode { get; set; }

        [Required, DataType(DataType.Date)]
        [Display(Name = "Inspected on")]
        public DateTime InspectedOn { get; set; }

        [Required, EmailAddress, StringLength(256)]
        [Display(Name = "Claim handler e-mail")]
        public string HandlerEmail { get; set; }

        [Range(0, 10000000)]
        [Display(Name = "Estimated loss (EUR)")]
        public decimal? EstimatedLoss { get; set; }

        [DataType(DataType.MultilineText), StringLength(4000)]
        public string Notes { get; set; }
    }

    [Serializable]
    public class RecentInspection
    {
        public int Id { get; set; }
        public string ClaimNumber { get; set; }
    }

    public class InspectionDetailsModel
    {
        public Inspection Inspection { get; set; }
        public IList<PhotoDto> Photos { get; set; }
        public IList<KeyValuePair<DateTime, string>> History { get; set; }
        public IList<string> Notices { get; set; }
        public bool CanEdit { get; set; }
        public string ReportBlockedReason { get; set; }
    }

    public static class Perils
    {
        public static readonly string[] Codes = { "FIR", "WAT", "STO", "FLO", "SUB", "THE", "IMP" };
    }
}

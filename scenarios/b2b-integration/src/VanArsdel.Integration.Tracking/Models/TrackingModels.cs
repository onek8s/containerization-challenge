using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using VanArsdel.Integration.Domain.Orders;
using VanArsdel.Integration.Monitoring;

namespace VanArsdel.Integration.Tracking.Models
{
    public class OrderSearchModel
    {
        [Display(Name = "Partner")]
        public string PartnerId { get; set; }

        [Display(Name = "PO number")]
        public string OrderNumber { get; set; }

        [Display(Name = "ERP order")]
        public string ErpOrderNumber { get; set; }

        [DataType(DataType.Date)]
        public DateTime? From { get; set; }

        [DataType(DataType.Date)]
        public DateTime? To { get; set; }

        public string Status { get; set; }

        public IList<OrderActivityRow> Results { get; set; } = new List<OrderActivityRow>();

        public IList<string> Partners { get; set; } = new List<string>();

        public bool Searched { get; set; }

        public static readonly string[] Statuses = { "Received", "Validated", "CreditHold", "SentToErp", "Confirmed", "Despatched", "Invoiced", "Rejected" };
    }

    public class OrderDetailsModel
    {
        public OrderActivityRow Order { get; set; }
        public IList<MilestoneStep> Steps { get; set; } = new List<MilestoneStep>();
        public IList<InterchangeRecord> Interchanges { get; set; } = new List<InterchangeRecord>();
    }

    public class MilestoneStep
    {
        public string Name { get; set; }
        public DateTime? AtUtc { get; set; }
        public string SinceReceived { get; set; }
        public bool Problem { get; set; }
    }

    public class KpiModel
    {
        [DataType(DataType.Date)]
        public DateTime From { get; set; }

        [DataType(DataType.Date)]
        public DateTime To { get; set; }

        public IList<PartnerKpi> Kpis { get; set; } = new List<PartnerKpi>();
    }

    public class InterchangeListModel
    {
        public string PartnerId { get; set; }
        public int Days { get; set; }
        public bool OverdueOnly { get; set; }
        public IList<InterchangeRecord> Interchanges { get; set; } = new List<InterchangeRecord>();
        public DateTime NowUtc { get; set; }
    }

    public class LoginModel
    {
        [Required]
        [Display(Name = "User name")]
        public string UserName { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Keep me signed in")]
        public bool RememberMe { get; set; }

        public string ReturnUrl { get; set; }
    }

    public class ChangePasswordModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string CurrentPassword { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [StringLength(128, MinimumLength = 10, ErrorMessage = "The new password must be at least 10 characters.")]
        [Display(Name = "New password")]
        public string NewPassword { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "The passwords do not match.")]
        [Display(Name = "Repeat new password")]
        public string ConfirmPassword { get; set; }
    }

    public class UserEditModel
    {
        [Required]
        [StringLength(64)]
        [RegularExpression(@"^[A-Za-z0-9._@-]+$", ErrorMessage = "Letters, digits and . _ @ - only.")]
        [Display(Name = "User name")]
        public string UserName { get; set; }

        [Required]
        [Display(Name = "Name")]
        public string DisplayName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Display(Name = "Partner (empty for VanArsdel staff)")]
        public string PartnerId { get; set; }

        public bool CustomerService { get; set; }
        public bool Admin { get; set; }
        public bool Locked { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Initial password (only when creating or resetting)")]
        public string InitialPassword { get; set; }

        public bool IsNew { get; set; }
    }
}

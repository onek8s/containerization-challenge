using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Wingtip.FieldSales.Data.Entities;

namespace Wingtip.FieldSales.Backoffice.Models
{
    public class LoginViewModel
    {
        [Required, Display(Name = "User name")]
        public string UserName { get; set; }

        [Required, DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Keep me signed in")]
        public bool RememberMe { get; set; }

        public string ReturnUrl { get; set; }
    }

    public class DashboardViewModel
    {
        public int PendingApprovals { get; set; }
        public int OrdersToday { get; set; }
        public decimal NetToday { get; set; }
        public DateTime? LastPriceListImport { get; set; }
        public long PriceListVersion { get; set; }
        public List<RepActivity> Reps { get; set; }
        public string ClientInstallUrl { get; set; }
    }

    public class RepActivity
    {
        public string RepCode { get; set; }
        public string SubmittedBy { get; set; }
        public int Orders { get; set; }
        public decimal NetTotal { get; set; }
        public DateTime LastReceived { get; set; }
    }

    public class OrderSearchViewModel
    {
        [DataType(DataType.Date)]
        public DateTime? From { get; set; }

        [DataType(DataType.Date)]
        public DateTime? To { get; set; }

        public string RepCode { get; set; }
        public string Customer { get; set; }
        public int? Status { get; set; }
        public List<SalesOrderEntity> Orders { get; set; }
    }

    public class DecisionViewModel
    {
        public int SalesOrderId { get; set; }
        public bool Approve { get; set; }

        [StringLength(400)]
        public string Comment { get; set; }
    }

    public class CustomerEditViewModel
    {
        public int CustomerId { get; set; }
        public string CustomerNo { get; set; }
        public string Name { get; set; }

        [EmailAddress, StringLength(200), Display(Name = "Confirmation e-mail")]
        public string Email { get; set; }

        [Required, RegularExpression("^[A-D]$", ErrorMessage = "Price group is A, B, C or D."), Display(Name = "Price group")]
        public string PriceGroup { get; set; }

        [Display(Name = "Sales rep")]
        public string RepCode { get; set; }

        public bool Monday { get; set; }
        public bool Tuesday { get; set; }
        public bool Wednesday { get; set; }
        public bool Thursday { get; set; }
        public bool Friday { get; set; }

        [Display(Name = "Blocked by credit control")]
        public bool CreditBlocked { get; set; }

        public IEnumerable<string> RepCodes { get; set; }
    }

    public class PriceListViewModel
    {
        public long PriceListVersion { get; set; }
        public List<PriceListImportEntity> Imports { get; set; }
        public List<PriceRuleEntity> Rules { get; set; }
        public List<PromotionEntity> Promotions { get; set; }
        public int ActiveProducts { get; set; }
        public List<string> ImportErrors { get; set; }
        public string ImportMessage { get; set; }
        public PromotionInput NewPromotion { get; set; }
    }

    public class PromotionInput
    {
        [Required, Display(Name = "Item")]
        public string ItemNo { get; set; }

        [Range(0.01, 100000), Display(Name = "Promotion price")]
        public decimal PromoPrice { get; set; }

        [DataType(DataType.Date), Display(Name = "From")]
        public DateTime ValidFrom { get; set; }

        [DataType(DataType.Date), Display(Name = "To")]
        public DateTime ValidTo { get; set; }
    }

    public class UserRow
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string DisplayName { get; set; }
        public string RepCode { get; set; }
        public decimal DiscountLimitPercent { get; set; }
        public string Roles { get; set; }
        public bool LockedOut { get; set; }
    }

    public class UserEditViewModel
    {
        public string Id { get; set; }

        [Required, StringLength(64), RegularExpression(@"^[a-z0-9._-]+$", ErrorMessage = "Use lower case letters, digits, dot, dash or underscore."), Display(Name = "User name")]
        public string UserName { get; set; }

        [Required, StringLength(100), Display(Name = "Name")]
        public string DisplayName { get; set; }

        [EmailAddress]
        public string Email { get; set; }

        [RegularExpression("^[A-Z]{2,4}$", ErrorMessage = "Rep code is 2-4 capital letters."), Display(Name = "Rep code")]
        public string RepCode { get; set; }

        [Range(0, 25), Display(Name = "Discount limit %")]
        public decimal DiscountLimitPercent { get; set; }

        [Display(Name = "Sales rep")]
        public bool IsSalesRep { get; set; }

        [Display(Name = "Sales manager")]
        public bool IsSalesManager { get; set; }

        [Display(Name = "Administrator")]
        public bool IsAdministrator { get; set; }

        [DataType(DataType.Password), Display(Name = "New password")]
        public string NewPassword { get; set; }
    }
}

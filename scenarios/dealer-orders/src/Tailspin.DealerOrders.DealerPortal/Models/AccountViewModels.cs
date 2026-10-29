using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Tailspin.DealerOrders.DealerPortal.Models
{
    public class LoginModel
    {
        [Required, Display(Name = "User name")]
        public string UserName { get; set; }

        [Required, DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Keep me signed in")]
        public bool RememberMe { get; set; }

        public string ReturnUrl { get; set; }
    }

    public class ForgotPasswordModel
    {
        [Required, EmailAddress]
        public string Email { get; set; }
    }

    public class ResetPasswordModel
    {
        [Required] public string UserId { get; set; }
        [Required] public string Code { get; set; }

        [Required, DataType(DataType.Password), Display(Name = "New password")]
        public string Password { get; set; }

        [DataType(DataType.Password), Display(Name = "Confirm password"), Compare("Password", ErrorMessage = "The passwords do not match.")]
        public string ConfirmPassword { get; set; }
    }

    public class ChangePasswordModel
    {
        [Required, DataType(DataType.Password), Display(Name = "Current password")]
        public string OldPassword { get; set; }

        [Required, DataType(DataType.Password), Display(Name = "New password")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password), Display(Name = "Confirm password"), Compare("NewPassword", ErrorMessage = "The passwords do not match.")]
        public string ConfirmPassword { get; set; }
    }

    public class UserViewModel
    {
        public string Id { get; set; }

        [Required, StringLength(64), Display(Name = "User name")]
        public string UserName { get; set; }

        [Required, StringLength(100), Display(Name = "Name")]
        public string DisplayName { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Display(Name = "Language")]
        public string LanguageCode { get; set; }

        public bool Disabled { get; set; }
        public DateTime? LastLoginUtc { get; set; }
        public bool IsLockedOut { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
    }
}

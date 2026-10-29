using System;
using System.ComponentModel.DataAnnotations;

namespace Fabrikam.Housing.Identity.Models
{
    public class LoginViewModel
    {
        [Required, Display(Name = "E-mail address or staff username")]
        public string UserName { get; set; }

        [Required, DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Keep me signed in on this device")]
        public bool RememberMe { get; set; }

        public string ReturnUrl { get; set; }
    }

    public class RegisterViewModel
    {
        [Required, Display(Name = "Tenancy reference"), RegularExpression(@"^\s*[Tt]\d{6}\s*$", ErrorMessage = "Your tenancy reference is a T followed by six digits, for example T020145. It is on your rent statement.")]
        public string TenancyReference { get; set; }

        [Required, DataType(DataType.Date), Display(Name = "Date of birth")]
        public DateTime? DateOfBirth { get; set; }

        [Required, Display(Name = "Postcode of your home")]
        public string Postcode { get; set; }

        [Required, EmailAddress, Display(Name = "E-mail address")]
        public string Email { get; set; }

        [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 10)]
        public string Password { get; set; }

        [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "The passwords do not match."), Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; }

        public string ReturnUrl { get; set; }
    }

    public class ForgotPasswordViewModel
    {
        [Required, EmailAddress, Display(Name = "E-mail address")]
        public string Email { get; set; }
    }

    public class ResetPasswordViewModel
    {
        [Required]
        public string UserId { get; set; }

        [Required]
        public string Token { get; set; }

        [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 10)]
        public string Password { get; set; }

        [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "The passwords do not match."), Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; }
    }

    public class LoggedOutViewModel
    {
        public string PostLogoutRedirectUri { get; set; }
        public string ClientName { get; set; }
        public string SignOutIframeUrl { get; set; }
    }

    public class ErrorViewModel
    {
        public string RequestId { get; set; }
        public string Error { get; set; }
        public string Description { get; set; }
    }
}

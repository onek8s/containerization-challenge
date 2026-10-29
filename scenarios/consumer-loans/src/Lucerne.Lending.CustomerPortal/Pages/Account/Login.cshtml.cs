using System.ComponentModel.DataAnnotations;
using Lucerne.Lending.CustomerPortal.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Lucerne.Lending.CustomerPortal.Pages.Account;

public sealed class LoginModel : PageModel
{
    private readonly SignInManager<PortalUser> _signIn;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(SignInManager<PortalUser> signIn, ILogger<LoginModel> logger)
    {
        _signIn = signIn;
        _logger = logger;
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public sealed class InputModel
    {
        [Required, EmailAddress] public string Email { get; set; } = "";
        [Required] public string Password { get; set; } = "";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var result = await _signIn.PasswordSignInAsync(Input.Email, Input.Password, isPersistent: false, lockoutOnFailure: true);
        if (result.RequiresTwoFactor) return RedirectToPage("LoginWith2fa", new { ReturnUrl });
        if (result.IsLockedOut)
        {
            _logger.LogWarning("Portal account {Email} locked out", Input.Email);
            ModelState.AddModelError("", "The account is locked for 15 minutes after too many attempts.");
            return Page();
        }
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "E-mail or password is incorrect.");
            return Page();
        }
        await AccountPages.TouchSignInAsync(_signIn, Input.Email);
        return LocalRedirect(AccountPages.SafeReturn(Url, ReturnUrl));
    }
}

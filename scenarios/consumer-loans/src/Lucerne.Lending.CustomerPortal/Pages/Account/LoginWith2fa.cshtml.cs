using System.ComponentModel.DataAnnotations;
using Lucerne.Lending.CustomerPortal.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Lucerne.Lending.CustomerPortal.Pages.Account;

public sealed class LoginWith2faModel : PageModel
{
    private readonly SignInManager<PortalUser> _signIn;

    public LoginWith2faModel(SignInManager<PortalUser> signIn) => _signIn = signIn;

    [BindProperty, Required] public string Code { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnGetAsync() =>
        await _signIn.GetTwoFactorAuthenticationUserAsync() == null ? RedirectToPage("Login") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _signIn.GetTwoFactorAuthenticationUserAsync();
        if (user == null) return RedirectToPage("Login");
        if (!ModelState.IsValid) return Page();
        var code = Code.Replace(" ", "").Replace("-", "");
        var result = await _signIn.TwoFactorAuthenticatorSignInAsync(code, isPersistent: false, rememberClient: false);
        if (result.IsLockedOut)
        {
            ModelState.AddModelError("", "The account is locked for 15 minutes after too many attempts.");
            return Page();
        }
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "The code is not valid.");
            return Page();
        }
        await AccountPages.TouchSignInAsync(_signIn, user.Email!);
        return LocalRedirect(AccountPages.SafeReturn(Url, ReturnUrl));
    }
}

using Lucerne.Lending.CustomerPortal.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Lucerne.Lending.CustomerPortal.Pages.Account;

[IgnoreAntiforgeryToken]
public sealed class LogoutModel : PageModel
{
    private readonly SignInManager<PortalUser> _signIn;

    public LogoutModel(SignInManager<PortalUser> signIn) => _signIn = signIn;

    public async Task<IActionResult> OnPostAsync()
    {
        await _signIn.SignOutAsync();
        return RedirectToPage();
    }
}

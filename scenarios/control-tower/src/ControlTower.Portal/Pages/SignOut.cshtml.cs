using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Proseware.ControlTower.Portal.Pages;

public class SignOutModel : PageModel
{
    public IActionResult OnGet() =>
        SignOut(new AuthenticationProperties { RedirectUri = "/track" }, CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme);
}

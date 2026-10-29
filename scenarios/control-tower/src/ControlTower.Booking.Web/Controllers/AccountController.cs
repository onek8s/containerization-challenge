using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Proseware.ControlTower.Booking.Controllers;

public class AccountController : Controller
{
    [Authorize]
    public IActionResult SignOutAll() =>
        SignOut(new AuthenticationProperties { RedirectUri = "/" }, CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme);

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}

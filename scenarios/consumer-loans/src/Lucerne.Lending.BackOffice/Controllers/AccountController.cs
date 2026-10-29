using System.Security.Claims;
using Lucerne.Lending.BackOffice.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.WsFederation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lucerne.Lending.BackOffice.Controllers;

[AllowAnonymous]
public sealed class AccountController : Controller
{
    private readonly IConfiguration _configuration;

    public AccountController(IConfiguration configuration) => _configuration = configuration;

    private bool DevelopmentAuth => _configuration.GetValue("Auth:Mode", "WsFederation") == "Development";

    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (!DevelopmentAuth) return Challenge(new AuthenticationProperties { RedirectUri = returnUrl ?? "/" }, WsFederationDefaults.AuthenticationScheme);
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    /// <summary>Development sign-in without ADFS (Auth:Mode = Development).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string user, string[] roles, string? returnUrl)
    {
        if (!DevelopmentAuth) return NotFound();
        var claims = new List<Claim> { new(ClaimTypes.Name, user), new(ClaimTypes.Upn, user + "@corp.local") };
        claims.AddRange(roles.Where(Roles.All.Contains).Select(r => new Claim(ClaimTypes.Role, r)));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, "Development")));
        return LocalRedirect(returnUrl ?? "/");
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (DevelopmentAuth) return RedirectToAction(nameof(Login));
        return SignOut(new AuthenticationProperties { RedirectUri = "/" }, WsFederationDefaults.AuthenticationScheme);
    }

    public IActionResult Denied() => View();
}

using System.Security.Claims;
using FieldService.Data;
using FieldService.Data.Services;
using FieldService.Web.Models;
using FieldService.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FieldService.Web.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly FieldServiceContext _db;
    private readonly ILogger<AccountController> _log;

    public AccountController(FieldServiceContext db, ILogger<AccountController> log)
    {
        _db = db;
        _log = log;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _db.Users.SingleOrDefaultAsync(u => u.UserName == model.UserName.ToLower());
        if (user == null || !PasswordHasher.Verify(model.Password, user.PasswordHash))
        {
            _log.LogWarning("Failed login for {User} from {Ip}", model.UserName, HttpContext.Connection.RemoteIpAddress);
            ModelState.AddModelError("", "Invalid user name or password.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.GivenName, user.DisplayName),
            new(ClaimTypes.Role, user.Role)
        };
        if (user.TechnicianId.HasValue)
        {
            claims.Add(new Claim(ClaimsExtensions.TechnicianClaim, user.TechnicianId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        _log.LogInformation("{User} signed in", user.UserName);

        return Url.IsLocalUrl(model.ReturnUrl) ? Redirect(model.ReturnUrl) : RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    public IActionResult Denied() => View();
}

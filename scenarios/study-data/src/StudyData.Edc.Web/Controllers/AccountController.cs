using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StudyData.Data;
using StudyData.Edc.Web.Models;
using StudyData.Edc.Web.Services;

namespace StudyData.Edc.Web.Controllers;

[Authorize]
[Route("account")]
public class AccountController : Controller
{
    private readonly SignInManager<SiteUser> _signIn;
    private readonly UserManager<SiteUser> _users;
    private readonly EdcOptions _options;
    private readonly ILogger<AccountController> _log;

    public AccountController(SignInManager<SiteUser> signIn, UserManager<SiteUser> users, IOptions<EdcOptions> options, ILogger<AccountController> log)
    {
        _signIn = signIn;
        _users = users;
        _options = options.Value;
        _log = log;
    }

    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl) => View(new LoginModel { ReturnUrl = returnUrl });

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _users.FindByEmailAsync(model.Email);
        if (user is null || !user.Active)
        {
            ModelState.AddModelError("", "Invalid e-mail address or password.");
            return View(model);
        }

        var result = await _signIn.PasswordSignInAsync(user, model.Password, isPersistent: false, lockoutOnFailure: true);
        if (result.RequiresTwoFactor)
        {
            return RedirectToAction(nameof(LoginWith2fa), new { model.ReturnUrl });
        }

        if (result.IsLockedOut)
        {
            _log.LogWarning("Account {Email} locked out", model.Email);
            ModelState.AddModelError("", "The account is locked for 30 minutes after too many failed attempts.");
            return View(model);
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "Invalid e-mail address or password.");
            return View(model);
        }

        if (_options.RequireTwoFactor && !user.TwoFactorEnabled)
        {
            return RedirectToAction(nameof(Authenticator));
        }

        return LocalRedirect(model.ReturnUrl ?? "/");
    }

    [AllowAnonymous]
    [HttpGet("login-2fa")]
    public async Task<IActionResult> LoginWith2fa(string? returnUrl)
    {
        if (await _signIn.GetTwoFactorAuthenticationUserAsync() is null)
        {
            return RedirectToAction(nameof(Login));
        }

        return View(new TwoFactorModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost("login-2fa")]
    public async Task<IActionResult> LoginWith2fa(TwoFactorModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var code = model.Code.Replace(" ", "").Replace("-", "");
        var result = await _signIn.TwoFactorAuthenticatorSignInAsync(code, false, rememberClient: false);
        if (result.Succeeded)
        {
            return LocalRedirect(model.ReturnUrl ?? "/");
        }

        ModelState.AddModelError("", result.IsLockedOut ? "The account is locked." : "The code is not valid.");
        return View(model);
    }

    [HttpGet("authenticator")]
    public async Task<IActionResult> Authenticator()
    {
        var user = await _users.GetUserAsync(User) ?? throw new InvalidOperationException();
        var key = await _users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await _users.ResetAuthenticatorKeyAsync(user);
            key = await _users.GetAuthenticatorKeyAsync(user);
        }

        return View(new AuthenticatorModel
        {
            SharedKey = Format(key!),
            Uri = $"otpauth://totp/{UrlEncoder.Default.Encode("Study Data")}:{UrlEncoder.Default.Encode(user.Email!)}?secret={key}&issuer={UrlEncoder.Default.Encode("Study Data")}&digits=6",
            Enabled = user.TwoFactorEnabled
        });
    }

    [HttpPost("authenticator")]
    public async Task<IActionResult> Authenticator(AuthenticatorModel model)
    {
        var user = await _users.GetUserAsync(User) ?? throw new InvalidOperationException();
        var ok = await _users.VerifyTwoFactorTokenAsync(user, _users.Options.Tokens.AuthenticatorTokenProvider, model.Code?.Replace(" ", "") ?? "");
        if (!ok)
        {
            ModelState.AddModelError("", "The code is not valid. Check the time on your phone and try again.");
            return await Authenticator();
        }

        await _users.SetTwoFactorEnabledAsync(user, true);
        await _signIn.RefreshSignInAsync(user);
        _log.LogInformation("Authenticator enabled for {Email}", user.Email);
        return Redirect("/");
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet("denied")]
    public IActionResult Denied() => View();

    private static string Format(string key)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4)
        {
            sb.Append(key.AsSpan(i, Math.Min(4, key.Length - i))).Append(' ');
        }

        return sb.ToString().TrimEnd().ToLowerInvariant();
    }
}

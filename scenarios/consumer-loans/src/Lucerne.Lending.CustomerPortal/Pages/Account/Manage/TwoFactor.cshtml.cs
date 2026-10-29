using System.Security.Claims;
using System.Text;
using Lucerne.Lending.CustomerPortal.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Lucerne.Lending.CustomerPortal.Pages.Account.Manage;

public sealed class TwoFactorModel : PageModel
{
    private readonly UserManager<PortalUser> _users;
    private readonly SignInManager<PortalUser> _signIn;
    private readonly PortalOptions _portal;
    private readonly ILogger<TwoFactorModel> _logger;

    public TwoFactorModel(UserManager<PortalUser> users, SignInManager<PortalUser> signIn, PortalOptions portal, ILogger<TwoFactorModel> logger)
    {
        _users = users;
        _signIn = signIn;
        _portal = portal;
        _logger = logger;
    }

    public bool Enabled { get; private set; }
    public string SharedKey { get; private set; } = "";
    public string AuthenticatorUri { get; private set; } = "";
    [BindProperty] public string Code { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return RedirectToPage("/Account/Login");
        await LoadAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return RedirectToPage("/Account/Login");
        var code = Code.Replace(" ", "").Replace("-", "");
        if (!await _users.VerifyTwoFactorTokenAsync(user, _users.Options.Tokens.AuthenticatorTokenProvider, code))
        {
            ModelState.AddModelError("", "The code is not valid. Check the time on your phone and try again.");
            await LoadAsync(user);
            return Page();
        }
        await _users.SetTwoFactorEnabledAsync(user, true);
        _logger.LogInformation("Authenticator enabled for portal user {UserId}", user.Id);
        // the code was just verified, so the session counts as strongly authenticated
        await _signIn.SignInWithClaimsAsync(user, isPersistent: false, new[] { new Claim("amr", "mfa") });
        return LocalRedirect(AccountPages.SafeReturn(Url, ReturnUrl));
    }

    public async Task<IActionResult> OnPostResetAsync()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return RedirectToPage("/Account/Login");
        await _users.SetTwoFactorEnabledAsync(user, false);
        await _users.ResetAuthenticatorKeyAsync(user);
        await _signIn.RefreshSignInAsync(user);
        _logger.LogInformation("Authenticator reset for portal user {UserId}", user.Id);
        return RedirectToPage();
    }

    private async Task LoadAsync(PortalUser user)
    {
        Enabled = user.TwoFactorEnabled;
        if (Enabled) return;
        var key = await _users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await _users.ResetAuthenticatorKeyAsync(user);
            key = await _users.GetAuthenticatorKeyAsync(user);
        }
        var sb = new StringBuilder();
        for (var i = 0; i < key!.Length; i += 4) sb.Append(key.AsSpan(i, Math.Min(4, key.Length - i))).Append(' ');
        SharedKey = sb.ToString().TrimEnd().ToLowerInvariant();
        AuthenticatorUri = $"otpauth://totp/{Uri.EscapeDataString(_portal.BankName)}:{Uri.EscapeDataString(user.Email!)}?secret={key}&issuer={Uri.EscapeDataString(_portal.BankName)}&digits=6";
    }
}

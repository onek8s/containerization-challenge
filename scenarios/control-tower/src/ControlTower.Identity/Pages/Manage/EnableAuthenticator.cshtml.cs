using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Proseware.ControlTower.Identity.Data;
using QRCoder;

namespace Proseware.ControlTower.Identity.Pages.Manage;

public class EnableAuthenticatorModel : PageModel
{
    private const string Issuer = "Proseware Freight";
    private readonly UserManager<ApplicationUser> _users;
    private readonly IdentityServer4.Services.IIdentityServerInteractionService _interaction;

    public EnableAuthenticatorModel(UserManager<ApplicationUser> users, IdentityServer4.Services.IIdentityServerInteractionService interaction)
    {
        _users = users;
        _interaction = interaction;
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Required { get; set; }

    [BindProperty, Required, StringLength(7, MinimumLength = 6)]
    public string Code { get; set; } = "";

    public string SharedKey { get; private set; } = "";
    public string QrCodeDataUri { get; private set; } = "";
    public IEnumerable<string>? RecoveryCodes { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Challenge();
        await LoadKeyAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Challenge();
        if (!ModelState.IsValid)
        {
            await LoadKeyAsync(user);
            return Page();
        }

        var code = Code.Replace(" ", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
        var valid = await _users.VerifyTwoFactorTokenAsync(user, _users.Options.Tokens.AuthenticatorTokenProvider, code);
        if (!valid)
        {
            ModelState.AddModelError(nameof(Code), "The code is not valid.");
            await LoadKeyAsync(user);
            return Page();
        }

        await _users.SetTwoFactorEnabledAsync(user, true);
        RecoveryCodes = await _users.GenerateNewTwoFactorRecoveryCodesAsync(user, 8);
        if (ReturnUrl is not null && !_interaction.IsValidReturnUrl(ReturnUrl) && !Url.IsLocalUrl(ReturnUrl))
            ReturnUrl = null;
        return Page();
    }

    private async Task LoadKeyAsync(ApplicationUser user)
    {
        var key = await _users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await _users.ResetAuthenticatorKeyAsync(user);
            key = await _users.GetAuthenticatorKeyAsync(user);
        }
        SharedKey = FormatKey(key!);
        var uri = $"otpauth://totp/{UrlEncoder.Default.Encode(Issuer)}:{UrlEncoder.Default.Encode(user.UserName)}?secret={key}&issuer={UrlEncoder.Default.Encode(Issuer)}&digits=6";
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
        QrCodeDataUri = "data:image/png;base64," + Convert.ToBase64String(new PngByteQRCode(data).GetGraphic(5));
    }

    private static string FormatKey(string key)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4)
            sb.Append(key.AsSpan(i, Math.Min(4, key.Length - i))).Append(' ');
        return sb.ToString().TrimEnd().ToLowerInvariant();
    }
}

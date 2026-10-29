using System.ComponentModel.DataAnnotations;
using IdentityServer4.Events;
using IdentityServer4.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Proseware.ControlTower.Identity.Data;

namespace Proseware.ControlTower.Identity.Pages.Account;

public class LoginWith2faModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IEventService _events;

    public LoginWith2faModel(SignInManager<ApplicationUser> signIn, IIdentityServerInteractionService interaction, IEventService events)
    {
        _signIn = signIn;
        _interaction = interaction;
        _events = events;
    }

    [BindProperty, Required, StringLength(7, MinimumLength = 6)]
    public string Code { get; set; } = "";

    [BindProperty]
    public bool RememberMachine { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool RememberMe { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (await _signIn.GetTwoFactorAuthenticationUserAsync() is null)
            return RedirectToPage("./Login", new { returnUrl = ReturnUrl });
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var user = await _signIn.GetTwoFactorAuthenticationUserAsync();
        if (user is null) return RedirectToPage("./Login", new { returnUrl = ReturnUrl });

        var code = Code.Replace(" ", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
        var result = await _signIn.TwoFactorAuthenticatorSignInAsync(code, RememberMe, RememberMachine);
        if (result.IsLockedOut) return RedirectToPage("./Lockout");
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid authenticator code.");
            return Page();
        }

        var context = await _interaction.GetAuthorizationContextAsync(ReturnUrl);
        await _events.RaiseAsync(new UserLoginSuccessEvent(user.UserName, user.Id, user.DisplayName, clientId: context?.Client.ClientId));
        if (context is not null || _interaction.IsValidReturnUrl(ReturnUrl) || Url.IsLocalUrl(ReturnUrl))
            return Redirect(ReturnUrl!);
        return Redirect("~/");
    }
}

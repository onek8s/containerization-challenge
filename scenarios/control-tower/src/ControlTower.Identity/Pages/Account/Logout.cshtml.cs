using IdentityServer4.Events;
using IdentityServer4.Extensions;
using IdentityServer4.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Proseware.ControlTower.Identity.Data;

namespace Proseware.ControlTower.Identity.Pages.Account;

[IgnoreAntiforgeryToken(Order = 1001)]
public class LogoutModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IEventService _events;

    public LogoutModel(SignInManager<ApplicationUser> signIn, IIdentityServerInteractionService interaction, IEventService events)
    {
        _signIn = signIn;
        _interaction = interaction;
        _events = events;
    }

    [BindProperty(SupportsGet = true)]
    public string? LogoutId { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated != true) return await SignOutAsync();
        var context = await _interaction.GetLogoutContextAsync(LogoutId);
        // Logout started by a client application does not need a confirmation.
        return context?.ShowSignoutPrompt == false ? await SignOutAsync() : Page();
    }

    public Task<IActionResult> OnPostAsync() => SignOutAsync();

    private async Task<IActionResult> SignOutAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            LogoutId ??= await _interaction.CreateLogoutContextAsync();
            await _signIn.SignOutAsync();
            await _events.RaiseAsync(new UserLogoutSuccessEvent(User.GetSubjectId(), User.GetDisplayName()));
        }
        return RedirectToPage("./LoggedOut", new { logoutId = LogoutId });
    }
}

using IdentityServer4.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Proseware.ControlTower.Identity.Pages.Account;

public class LoggedOutModel : PageModel
{
    private readonly IIdentityServerInteractionService _interaction;

    public LoggedOutModel(IIdentityServerInteractionService interaction) => _interaction = interaction;

    public string? PostLogoutRedirectUri { get; private set; }
    public string? ClientName { get; private set; }
    public string? SignOutIframeUrl { get; private set; }

    public async Task OnGetAsync(string? logoutId)
    {
        var context = await _interaction.GetLogoutContextAsync(logoutId);
        PostLogoutRedirectUri = context?.PostLogoutRedirectUri;
        ClientName = string.IsNullOrEmpty(context?.ClientName) ? context?.ClientId : context.ClientName;
        SignOutIframeUrl = context?.SignOutIFrameUrl;
    }
}

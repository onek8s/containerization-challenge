using System.Diagnostics;
using IdentityServer4.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Proseware.ControlTower.Identity.Pages;

public class ErrorModel : PageModel
{
    private readonly IIdentityServerInteractionService _interaction;

    public ErrorModel(IIdentityServerInteractionService interaction) => _interaction = interaction;

    public string? Message { get; private set; }
    public string? Description { get; private set; }
    public string? RequestId { get; private set; }

    public async Task OnGetAsync(string? errorId)
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (errorId is null) return;
        var error = await _interaction.GetErrorContextAsync(errorId);
        if (error is null) return;
        Message = error.Error;
        Description = error.ErrorDescription;
        RequestId = error.RequestId ?? RequestId;
    }
}

using ContosoFoods.TradeRebates.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ContosoFoods.TradeRebates.Portal.Pages;

public abstract class PortalPageModel : PageModel
{
    protected string Actor => User.Identity?.Name ?? "unknown";

    /// <summary>Runs an action; business rule errors from the database become a message instead of an error page.</summary>
    protected async Task<IActionResult> TryAsync(Func<Task<IActionResult>> action, Func<IActionResult> onRuleError)
    {
        try
        {
            return await action();
        }
        catch (RebateRuleException ex)
        {
            TempData["Error"] = ex.Message;
            return onRuleError();
        }
    }
}

using Coho.Billing.Lookup.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Coho.Billing.Lookup.Pages;

public class BillDetailsModel : PageModel
{
    private readonly BillingQueries _queries;
    private readonly EngineClient _engine;

    public BillDetailsModel(BillingQueries queries, EngineClient engine)
    {
        _queries = queries;
        _engine = engine;
    }

    public BillView Bill { get; private set; }
    public IReadOnlyList<BillLineView> Lines { get; private set; }
    public IReadOnlyList<ReprintView> Reprints { get; private set; }

    [TempData]
    public string Message { get; set; }

    [TempData]
    public bool Failed { get; set; }

    public async Task<IActionResult> OnGetAsync(string billNumber)
    {
        (Bill, Lines, Reprints) = await _queries.GetBillAsync(billNumber);
        return Bill == null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostAsync(string billNumber, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            Message = "Enter a reason for the reprint.";
            Failed = true;
            return RedirectToPage(new { billNumber });
        }

        var error = await _engine.RequestReprintAsync(billNumber, User.Identity?.Name, reason.Trim());
        Message = error ?? "Reprint requested.";
        Failed = error != null;
        return RedirectToPage(new { billNumber });
    }
}

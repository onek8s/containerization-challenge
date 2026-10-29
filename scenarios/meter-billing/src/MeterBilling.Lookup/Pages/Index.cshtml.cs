using Coho.Billing.Lookup.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Coho.Billing.Lookup.Pages;

public class IndexModel : PageModel
{
    private readonly BillingQueries _queries;

    public IndexModel(BillingQueries queries)
    {
        _queries = queries;
    }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string Query { get; set; }

    public IReadOnlyList<AccountHit> Hits { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(Query))
        {
            return Page();
        }

        Hits = await _queries.SearchAsync(Query);
        if (Hits.Count == 1)
        {
            return RedirectToPage("/Accounts/Details", new { accountNumber = Hits[0].AccountNumber });
        }

        return Page();
    }
}

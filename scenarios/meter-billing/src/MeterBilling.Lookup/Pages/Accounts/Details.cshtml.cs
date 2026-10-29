using Coho.Billing.Lookup.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Coho.Billing.Lookup.Pages;

public class DetailsModel : PageModel
{
    private readonly BillingQueries _queries;

    public DetailsModel(BillingQueries queries)
    {
        _queries = queries;
    }

    public AccountView Account { get; private set; }
    public IReadOnlyList<BillView> Bills { get; private set; }
    public IReadOnlyList<ReadView> Reads { get; private set; }

    public async Task<IActionResult> OnGetAsync(string accountNumber)
    {
        Account = await _queries.GetAccountAsync(accountNumber);
        if (Account == null)
        {
            return NotFound();
        }

        Bills = await _queries.GetBillsAsync(accountNumber);
        Reads = await _queries.GetReadsAsync(Account.MeterSerial);
        return Page();
    }

    public static string Mask(string iban)
    {
        if (string.IsNullOrEmpty(iban) || iban.Length < 8) return iban;
        return iban[..4] + new string('*', iban.Length - 8) + iban[^4..];
    }
}

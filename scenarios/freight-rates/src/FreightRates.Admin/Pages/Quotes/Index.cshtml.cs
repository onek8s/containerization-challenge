using FreightRates.Data;
using FreightRates.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FreightRates.Admin.Pages.Quotes;

public class IndexModel : PageModel
{
    private readonly FreightRatesContext _db;

    public IndexModel(FreightRatesContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public string? Customer { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Number { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? From { get; set; }

    public List<string> CustomerCodes { get; private set; } = new();
    public List<Quote> Quotes { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken ct)
    {
        CustomerCodes = await _db.Customers.AsNoTracking().OrderBy(c => c.Code).Select(c => c.Code).ToListAsync(ct);

        var query = _db.Quotes.AsNoTracking().Include(q => q.Customer).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Customer))
            query = query.Where(q => q.Customer!.Code == Customer);
        if (!string.IsNullOrWhiteSpace(Number))
            query = query.Where(q => q.Number.StartsWith(Number.Trim()));
        if (From is { } from)
        {
            var fromUtc = from.Date.ToUniversalTime();
            query = query.Where(q => q.CreatedUtc >= fromUtc);
        }

        Quotes = await query.OrderByDescending(q => q.Id).Take(200).ToListAsync(ct);
    }
}

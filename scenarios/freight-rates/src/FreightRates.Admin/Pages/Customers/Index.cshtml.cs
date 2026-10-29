using FreightRates.Data;
using FreightRates.Data.Entities;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FreightRates.Admin.Pages.Customers;

public class IndexModel : PageModel
{
    private readonly FreightRatesContext _db;

    public IndexModel(FreightRatesContext db) => _db = db;

    public List<Customer> Customers { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken ct) =>
        Customers = await _db.Customers.AsNoTracking().OrderBy(c => c.Code).ToListAsync(ct);
}

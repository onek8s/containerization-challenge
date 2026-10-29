using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PartsCatalog.Data;

namespace PartsCatalog.Pages;

public class IndexModel : PageModel
{
    private readonly CatalogContext _db;

    public IndexModel(CatalogContext db)
    {
        _db = db;
    }

    public int TotalParts { get; private set; }
    public int TotalUnits { get; private set; }
    public decimal StockValue { get; private set; }
    public int ReorderCount { get; private set; }

    public async Task OnGetAsync()
    {
        var parts = await _db.Parts.AsNoTracking().ToListAsync();
        TotalParts = parts.Count;
        TotalUnits = parts.Sum(p => p.QuantityOnHand);
        StockValue = parts.Sum(p => p.QuantityOnHand * p.UnitPrice);
        ReorderCount = parts.Count(p => p.NeedsReorder);
    }
}

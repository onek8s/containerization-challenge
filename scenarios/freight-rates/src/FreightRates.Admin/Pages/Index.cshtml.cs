using FreightRates.Core;
using FreightRates.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FreightRates.Admin.Pages;

public class IndexModel : PageModel
{
    private readonly FreightRatesContext _db;
    private readonly IRatingEngine _engine;

    public IndexModel(FreightRatesContext db, IRatingEngine engine)
    {
        _db = db;
        _engine = engine;
    }

    public TariffInfo? Tariff { get; private set; }
    public string? TariffError { get; private set; }
    public int QuotesToday { get; private set; }
    public decimal QuotedToday { get; private set; }
    public int ActiveCustomers { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            Tariff = await _engine.GetTariffInfoAsync(ct);
        }
        catch (RatingException ex)
        {
            TariffError = ex.Message;
        }

        var since = DateTime.UtcNow.Date;
        var totals = await _db.Quotes.AsNoTracking().Where(q => q.CreatedUtc >= since).Select(q => q.Total).ToListAsync(ct);
        QuotesToday = totals.Count;
        QuotedToday = totals.Sum();
        ActiveCustomers = await _db.Customers.CountAsync(c => c.Active, ct);
    }
}

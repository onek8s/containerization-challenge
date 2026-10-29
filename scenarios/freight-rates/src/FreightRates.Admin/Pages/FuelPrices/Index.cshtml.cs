using System.ComponentModel.DataAnnotations;
using FreightRates.Data;
using FreightRates.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using CoreFuel = FreightRates.Core.FuelSurcharge;

namespace FreightRates.Admin.Pages.FuelPrices;

public class IndexModel : PageModel
{
    private readonly FreightRatesContext _db;

    public IndexModel(FreightRatesContext db) => _db = db;

    [BindProperty, Required]
    public DateOnly? WeekStarting { get; set; }

    [BindProperty, Range(0.5, 10)]
    public decimal DieselPrice { get; set; }

    public List<(DateOnly Week, decimal Price, decimal Percent)> Rows { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken ct)
    {
        WeekStarting = CoreFuel.WeekOf(DateOnly.FromDateTime(DateTime.Today));
        await LoadAsync(ct);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (WeekStarting is { } week && week.DayOfWeek != DayOfWeek.Monday)
            ModelState.AddModelError(nameof(WeekStarting), "The week must start on a Monday.");
        if (!ModelState.IsValid)
        {
            await LoadAsync(ct);
            return Page();
        }

        var existing = await _db.FuelPrices.FirstOrDefaultAsync(p => p.WeekStarting == WeekStarting, ct);
        if (existing is null)
            _db.FuelPrices.Add(new FuelPrice { WeekStarting = WeekStarting!.Value, DieselPrice = DieselPrice });
        else
            existing.DieselPrice = DieselPrice;
        await _db.SaveChangesAsync(ct);

        TempData["Message"] = $"Diesel price for week {WeekStarting:yyyy-MM-dd} saved.";
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        var steps = (await _db.FuelSurchargeSteps.AsNoTracking().ToListAsync(ct))
            .Select(s => new Core.FuelSurchargeStep(s.DieselPriceFrom, s.Percent))
            .ToList();
        var prices = await _db.FuelPrices.AsNoTracking().OrderByDescending(p => p.WeekStarting).Take(26).ToListAsync(ct);
        Rows = prices.Select(p => (p.WeekStarting, p.DieselPrice, CoreFuel.PercentFor(p.DieselPrice, steps))).ToList();
    }
}

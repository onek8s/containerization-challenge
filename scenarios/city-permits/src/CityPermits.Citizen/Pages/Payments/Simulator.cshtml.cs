using CityPermits.Citizen.Services;
using CityPermits.Data;
using CityPermits.Domain.Model;
using CityPermits.Integration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CityPermits.Citizen.Pages.Payments;

public class SimulatorModel : PageModel
{
    private readonly PermitsDbContext _db;
    private readonly CaseService _cases;
    private readonly PaystreamOptions _options;

    public SimulatorModel(PermitsDbContext db, CaseService cases, PaystreamOptions options) { _db = db; _cases = cases; _options = options; }

    [BindProperty(SupportsGet = true)] public string? Session { get; set; }
    public Payment? Payment { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_options.Mode != "Simulator") return NotFound();
        Payment = await _db.Payments.FirstOrDefaultAsync(p => p.SessionId == Session);
        return Payment is null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostAsync(string outcome)
    {
        if (_options.Mode != "Simulator") return NotFound();
        var p = await _db.Payments.FirstAsync(x => x.SessionId == Session);
        var c = await _db.Cases.Include(x => x.Fees).Include(x => x.Payments).Include(x => x.Events).FirstAsync(x => x.Id == p.CaseId);
        if (outcome == "ok")
        {
            p.CardBrand = "visa"; p.Last4 = "4242";
            await _cases.MarkPaidAsync(p, c);
        }
        else
        {
            p.Status = PaymentStatus.Failed; p.FailureReason = "card_declined";
            await _db.SaveChangesAsync();
        }
        return RedirectToPage("/Cases/Details", new { id = c.Id });
    }
}

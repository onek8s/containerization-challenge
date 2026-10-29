using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Portal.Services;

namespace Proseware.ControlTower.Portal.Pages;

public class BookModel : PageModel
{
    private readonly ControlTowerDbContext _db;
    private readonly BookingRequestService _requests;
    private readonly CustomerContext _customer;

    public BookModel(ControlTowerDbContext db, BookingRequestService requests, CustomerContext customer)
    {
        _db = db;
        _requests = requests;
        _customer = customer;
    }

    [BindProperty]
    public BookingRequest Request { get; set; } = new();

    public IReadOnlyList<string> Errors { get; private set; } = Array.Empty<string>();

    public IEnumerable<SelectListItem> Locations { get; private set; } = Array.Empty<SelectListItem>();

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
        var own = await _db.Locations.AsNoTracking().Where(l => l.CustomerId == _customer.RequiredCustomerId).Select(l => l.Id).FirstOrDefaultAsync(ct);
        if (own != 0) Request.OriginId = own;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        Errors = ModelState.IsValid ? _requests.Validate(Request) : new[] { "Check the numeric fields." };
        if (Errors.Count > 0)
        {
            await LoadAsync(ct);
            return Page();
        }
        var shipment = await _requests.CreateAsync(_customer.RequiredCustomerId, Request, _customer.UserName, ct);
        TempData["Message"] = $"Thank you. Your booking request {shipment.Number} has been received.";
        return RedirectToPage("/Shipments/Details", new { id = shipment.Id });
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        var customerId = _customer.RequiredCustomerId;
        var list = await _db.Locations.AsNoTracking()
            .Where(l => l.CustomerId == customerId || l.CustomerId == null)
            .OrderByDescending(l => l.CustomerId == customerId).ThenBy(l => l.CountryCode).ThenBy(l => l.City)
            .Select(l => new { l.Id, l.Name, l.City, l.CountryCode, Own = l.CustomerId == customerId })
            .ToListAsync(ct);
        Locations = list.Select(l => new SelectListItem($"{(l.Own ? "* " : "")}{l.Name}, {l.City} {l.CountryCode}", l.Id.ToString()));
    }
}

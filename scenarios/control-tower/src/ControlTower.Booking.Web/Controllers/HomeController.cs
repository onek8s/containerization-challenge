using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Booking.Models;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Customs;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Booking.Controllers;

[Authorize(Policy = Policies.Staff)]
public class HomeController : Controller
{
    private readonly ControlTowerDbContext _db;
    private readonly ShipmentQueries _queries;
    private readonly IClock _clock;

    public HomeController(ControlTowerDbContext db, ShipmentQueries queries, IClock clock)
    {
        _db = db;
        _queries = queries;
        _clock = clock;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var today = new DateTimeOffset(_clock.UtcNow.UtcDateTime.Date, TimeSpan.Zero);
        var tomorrow = today.AddDays(1);
        var open = _db.Shipments.Where(s => s.Status != ShipmentStatus.Delivered && s.Status != ShipmentStatus.Cancelled);
        var model = new DashboardViewModel
        {
            OpenShipments = await open.CountAsync(ct),
            Requested = await open.CountAsync(s => s.Status == ShipmentStatus.Requested, ct),
            PickupsToday = await open.CountAsync(s => s.PlannedPickup >= today && s.PlannedPickup < tomorrow, ct),
            DeliveriesToday = await open.CountAsync(s => s.PlannedDelivery >= today && s.PlannedDelivery < tomorrow, ct),
            OpenAlerts = await _db.Alerts.CountAsync(a => a.ResolvedAt == null, ct),
            CustomsPending = await _db.CustomsDeclarations.CountAsync(d => d.Status == DeclarationStatus.Draft || d.Status == DeclarationStatus.Rejected || d.Status == DeclarationStatus.UnderControl, ct),
            Recent = (await _queries.SearchAsync(new ShipmentFilter(OpenOnly: true, PageSize: 15), ct)).Items
        };
        return View(model);
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}

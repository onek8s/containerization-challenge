using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Portal.Services;

namespace Proseware.ControlTower.Portal.Pages;

[AllowAnonymous]
public class TrackModel : PageModel
{
    private readonly PortalShipments _shipments;

    public TrackModel(PortalShipments shipments) => _shipments = shipments;

    [BindProperty(SupportsGet = true)]
    public string? Number { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Postcode { get; set; }

    public bool Searched { get; private set; }

    public Shipment? Shipment { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Number) || string.IsNullOrWhiteSpace(Postcode)) return;
        Searched = true;
        Shipment = await _shipments.TrackAsync(Number, Postcode, ct);
    }
}

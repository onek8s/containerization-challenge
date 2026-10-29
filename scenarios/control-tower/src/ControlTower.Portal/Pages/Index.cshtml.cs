using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Portal.Services;

namespace Proseware.ControlTower.Portal.Pages;

public class IndexModel : PageModel
{
    private readonly PortalShipments _shipments;
    private readonly CustomerContext _customer;

    public IndexModel(PortalShipments shipments, CustomerContext customer)
    {
        _shipments = shipments;
        _customer = customer;
    }

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public string View { get; set; } = "open";

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public PagedResult<ShipmentRow> Result { get; private set; } = new(Array.Empty<ShipmentRow>(), 0, 1, 25);

    public async Task OnGetAsync(CancellationToken ct) =>
        Result = await _shipments.SearchAsync(_customer.RequiredCustomerId, Q, View != "all", PageNumber, ct);
}

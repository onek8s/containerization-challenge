using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Proseware.ControlTower.Documents;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Documents;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Portal.Services;

namespace Proseware.ControlTower.Portal.Pages.Shipments;

public class DetailsModel : PageModel
{
    private readonly PortalShipments _shipments;
    private readonly CustomerContext _customer;
    private readonly IDocumentStore _documents;

    public DetailsModel(PortalShipments shipments, CustomerContext customer, IDocumentStore documents)
    {
        _shipments = shipments;
        _customer = customer;
        _documents = documents;
    }

    public Shipment Shipment { get; private set; } = null!;
    public IReadOnlyList<StoredDocument> Documents { get; private set; } = Array.Empty<StoredDocument>();
    public (double Lat, double Lon, DateTimeOffset At)? VehiclePosition { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var shipment = await _shipments.GetAsync(_customer.RequiredCustomerId, id, ct);
        if (shipment is null) return NotFound();
        Shipment = shipment;
        Documents = await _shipments.DocumentsAsync(_customer.RequiredCustomerId, id, ct);

        // Only while the goods are on a truck: the position of the vehicle on the current road leg.
        var leg = shipment.Legs.FirstOrDefault(l => l.Status == LegStatus.Departed && l.Vehicle?.LastPosition is not null);
        if (leg?.Vehicle is { LastPosition: { } p, LastPositionAt: { } at } && at > DateTimeOffset.UtcNow.AddHours(-12))
            VehiclePosition = (p.Y, p.X, at);
        return Page();
    }

    public async Task<IActionResult> OnGetDocumentAsync(int id, Guid documentId, CancellationToken ct)
    {
        var doc = await _shipments.DocumentAsync(_customer.RequiredCustomerId, documentId, ct);
        if (doc is null || doc.ShipmentId != id) return NotFound();
        var stream = await _documents.OpenAsync(documentId, ct);
        return stream is null ? NotFound() : File(stream, doc.ContentType, doc.FileName);
    }
}

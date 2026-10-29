using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain.Documents;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Portal.Services;

/// <summary>Customer-scoped reads; every query filters on the signed-in customer.</summary>
public sealed class PortalShipments
{
    private readonly ControlTowerDbContext _db;
    private readonly ShipmentQueries _queries;

    public PortalShipments(ControlTowerDbContext db, ShipmentQueries queries)
    {
        _db = db;
        _queries = queries;
    }

    public Task<PagedResult<ShipmentRow>> SearchAsync(int customerId, string? q, bool openOnly, int page, CancellationToken ct) =>
        _queries.SearchAsync(new ShipmentFilter(CustomerId: customerId, Search: q, OpenOnly: openOnly, Page: page, PageSize: 25), ct);

    public async Task<Shipment?> GetAsync(int customerId, int shipmentId, CancellationToken ct)
    {
        var s = await _queries.GetAsync(shipmentId, ct);
        return s is not null && s.CustomerId == customerId ? s : null;
    }

    public async Task<IReadOnlyList<StoredDocument>> DocumentsAsync(int customerId, int shipmentId, CancellationToken ct) =>
        await _db.Documents.AsNoTracking()
            .Where(d => d.ShipmentId == shipmentId && d.VisibleToCustomer && _db.Shipments.Any(s => s.Id == shipmentId && s.CustomerId == customerId))
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

    public async Task<StoredDocument?> DocumentAsync(int customerId, Guid documentId, CancellationToken ct) =>
        await _db.Documents.AsNoTracking()
            .Where(d => d.Id == documentId && d.VisibleToCustomer && _db.Shipments.Any(s => s.Id == d.ShipmentId && s.CustomerId == customerId))
            .FirstOrDefaultAsync(ct);

    /// <summary>Public tracking: house number and the delivery postcode must both match.</summary>
    public async Task<Shipment?> TrackAsync(string number, string postalCode, CancellationToken ct)
    {
        if (!ShipmentNumber.IsValid(number?.Trim().ToUpperInvariant())) return null;
        var s = await _queries.GetByNumberAsync(number!.Trim().ToUpperInvariant(), ct);
        if (s?.Destination is null) return null;
        static string Norm(string v) => new(v.Where(char.IsLetterOrDigit).ToArray());
        return string.Equals(Norm(s.Destination.PostalCode), Norm(postalCode ?? ""), StringComparison.OrdinalIgnoreCase) ? s : null;
    }
}

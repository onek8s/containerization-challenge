using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Data;

public sealed record ShipmentFilter(
    int? CustomerId = null,
    string? Search = null,
    ShipmentStatus? Status = null,
    TransportMode? Mode = null,
    bool OpenOnly = false,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 50);

public sealed record ShipmentRow(
    int Id,
    string Number,
    string Customer,
    string? CustomerReference,
    TransportMode Mode,
    ShipmentStatus Status,
    string Origin,
    string Destination,
    DateTimeOffset PlannedPickup,
    DateTimeOffset PlannedDelivery,
    DateTimeOffset? Eta,
    int OpenAlerts);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public int Pages => PageSize == 0 ? 0 : (Total + PageSize - 1) / PageSize;
}

/// <summary>Read side shared by the web applications.</summary>
public sealed class ShipmentQueries
{
    private readonly ControlTowerDbContext _db;

    public ShipmentQueries(ControlTowerDbContext db) => _db = db;

    public async Task<PagedResult<ShipmentRow>> SearchAsync(ShipmentFilter f, CancellationToken ct = default)
    {
        var q = _db.Shipments.AsNoTracking().AsQueryable();
        if (f.CustomerId is { } cid) q = q.Where(s => s.CustomerId == cid);
        if (f.Status is { } st) q = q.Where(s => s.Status == st);
        if (f.Mode is { } m) q = q.Where(s => s.Mode == m);
        if (f.OpenOnly) q = q.Where(s => s.Status != ShipmentStatus.Delivered && s.Status != ShipmentStatus.Cancelled);
        if (f.From is { } from) q = q.Where(s => s.PlannedPickup >= from);
        if (f.To is { } to) q = q.Where(s => s.PlannedPickup < to);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim();
            q = q.Where(s => s.Number.Contains(term)
                             || (s.CustomerReference != null && s.CustomerReference.Contains(term))
                             || s.ConsigneeName.Contains(term)
                             || s.Packages.Any(p => p.Sscc == term)
                             || s.Legs.Any(l => l.CarrierReference == term));
        }

        var total = await q.CountAsync(ct);
        var pageSize = Math.Clamp(f.PageSize, 1, 500);
        var page = Math.Max(1, f.Page);
        var rows = await q
            .OrderByDescending(s => s.PlannedPickup)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new ShipmentRow(
                s.Id, s.Number, s.Customer!.Name, s.CustomerReference, s.Mode, s.Status,
                s.Origin!.City + ", " + s.Origin.CountryCode,
                s.Destination!.City + ", " + s.Destination.CountryCode,
                s.PlannedPickup, s.PlannedDelivery, s.Eta,
                _db.Alerts.Count(a => a.ShipmentId == s.Id && a.ResolvedAt == null)))
            .ToListAsync(ct);
        return new PagedResult<ShipmentRow>(rows, total, page, pageSize);
    }

    public Task<Shipment?> GetAsync(int id, CancellationToken ct = default) => Load().FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<Shipment?> GetByNumberAsync(string number, CancellationToken ct = default) => Load().FirstOrDefaultAsync(s => s.Number == number, ct);

    /// <summary>Finds the shipment a carrier status refers to: house number, carrier reference or package SSCC.</summary>
    public async Task<Shipment?> FindForStatusAsync(string? number, string? carrierReference, string? sscc, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(number))
        {
            var s = await Load(tracking: true).FirstOrDefaultAsync(x => x.Number == number, ct);
            if (s is not null) return s;
        }
        if (!string.IsNullOrEmpty(carrierReference))
        {
            var s = await Load(tracking: true).FirstOrDefaultAsync(x => x.Legs.Any(l => l.CarrierReference == carrierReference), ct);
            if (s is not null) return s;
        }
        if (!string.IsNullOrEmpty(sscc))
            return await Load(tracking: true).FirstOrDefaultAsync(x => x.Packages.Any(p => p.Sscc == sscc), ct);
        return null;
    }

    public IQueryable<Shipment> Load(bool tracking = false)
    {
        var q = _db.Shipments
            .Include(s => s.Customer)
            .Include(s => s.Origin)
            .Include(s => s.Destination)
            .Include(s => s.Packages)
            .Include(s => s.Legs).ThenInclude(l => l.Carrier)
            .Include(s => s.Legs).ThenInclude(l => l.From)
            .Include(s => s.Legs).ThenInclude(l => l.To)
            .Include(s => s.Legs).ThenInclude(l => l.Vehicle)
            .Include(s => s.Milestones)
            .Include(s => s.Charges)
            .Include(s => s.CustomsDeclarations).ThenInclude(d => d.Lines)
            .AsSplitQuery();
        return tracking ? q : q.AsNoTracking();
    }
}

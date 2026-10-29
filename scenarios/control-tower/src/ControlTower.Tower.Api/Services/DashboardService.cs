using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Locations;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Tower.Models;

namespace Proseware.ControlTower.Tower.Services;

public sealed class DashboardService
{
    /// <summary>A vehicle that has not reported for this long is shown as stale on the map.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);

    private readonly ControlTowerDbContext _db;
    private readonly IClock _clock;

    public DashboardService(ControlTowerDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var today = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var weekAgo = today.AddDays(-7);

        var open = await _db.Shipments.AsNoTracking()
            .Where(s => s.Status != ShipmentStatus.Delivered && s.Status != ShipmentStatus.Cancelled)
            .Select(s => new { s.Status, s.Mode, s.PlannedDelivery, s.Eta })
            .ToListAsync(ct);

        var recent = await _db.Shipments.AsNoTracking()
            .Include(s => s.Destination)
            .Where(s => (s.ActualDelivery != null && s.ActualDelivery >= weekAgo) || (s.PlannedPickup >= weekAgo && s.PlannedPickup < now))
            .ToListAsync(ct);

        var kpi = new Domain.Kpi.KpiCalculator();
        var delivered = recent.Where(s => s.ActualDelivery >= weekAgo).ToList();
        var pickupsDue = recent.Where(s => s.PlannedPickup >= weekAgo && s.PlannedPickup < now && s.Status != ShipmentStatus.Cancelled).ToList();

        var alerts = await _db.Alerts.AsNoTracking()
            .Where(a => a.ResolvedAt == null)
            .GroupBy(a => a.Severity)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var staleLimit = now - StaleAfter;
        var reporting = await _db.Vehicles.CountAsync(v => v.LastPositionAt != null && v.LastPositionAt >= staleLimit, ct);

        return new DashboardSummary(
            open.Count,
            open.GroupBy(s => s.Status).OrderBy(g => g.Key).Select(g => new CountItem(g.Key.ToString(), g.Count())).ToList(),
            open.GroupBy(s => s.Mode).OrderBy(g => g.Key).Select(g => new CountItem(g.Key.ToString(), g.Count())).ToList(),
            delivered.Count == 0 ? 0 : Math.Round(100.0 * delivered.Count(kpi.IsDeliveredOnTime) / delivered.Count, 1),
            pickupsDue.Count == 0 ? 0 : Math.Round(100.0 * pickupsDue.Count(s => s.ActualPickup is { } p && p <= s.PlannedPickup + kpi.PickupGrace) / pickupsDue.Count, 1),
            delivered.Count(s => s.ActualDelivery >= today),
            recent.Count(s => s.PlannedPickup >= today && s.Status == ShipmentStatus.Booked),
            open.Count(s => (s.Eta ?? s.PlannedDelivery) > s.PlannedDelivery.AddHours(2) || (s.Eta is null && s.PlannedDelivery < now)),
            Enum.GetValues<Severity>().Select(sev => new CountItem(sev.ToString(), alerts.FirstOrDefault(a => a.Key == sev)?.Count ?? 0)).ToList(),
            reporting,
            now);
    }

    public async Task<MapData> GetMapAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;

        var openLegs = await _db.Legs.AsNoTracking()
            .Include(l => l.Carrier).Include(l => l.From).Include(l => l.To).Include(l => l.Vehicle)
            .Where(l => l.Status == LegStatus.Departed || ((l.Status == LegStatus.Planned || l.Status == LegStatus.Confirmed) && l.PlannedDeparture < now.AddHours(24)))
            .Join(_db.Shipments.Where(s => s.Status != ShipmentStatus.Delivered && s.Status != ShipmentStatus.Cancelled),
                l => l.ShipmentId, s => s.Id, (l, s) => new { Leg = l, s.Number, s.TemperatureMin, s.TemperatureMax })
            .ToListAsync(ct);

        var legs = openLegs.Select(x => new MapLeg(
            x.Leg.Id, x.Leg.ShipmentId, x.Number, x.Leg.Mode, x.Leg.Status, x.Leg.Carrier?.Name, x.Leg.Vehicle?.Plate,
            x.Leg.From!.Position.Latitude(), x.Leg.From.Position.Longitude(), x.Leg.From.Name,
            x.Leg.To!.Position.Latitude(), x.Leg.To.Position.Longitude(), x.Leg.To.Name,
            x.Leg.PlannedArrival, x.Leg.Eta,
            (x.Leg.Eta ?? x.Leg.PlannedArrival) > x.Leg.PlannedArrival.AddHours(1) || (x.Leg.Eta is null && x.Leg.PlannedArrival < now)))
            .ToList();

        var vehicleShipments = openLegs.Where(x => x.Leg.Status == LegStatus.Departed && x.Leg.VehicleId != null)
            .GroupBy(x => x.Leg.VehicleId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var vehicles = await _db.Vehicles.AsNoTracking().ToListAsync(ct);
        var carriers = await _db.Carriers.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var mapVehicles = vehicles.Select(v =>
        {
            vehicleShipments.TryGetValue(v.Id, out var active);
            var outOfRange = active is not null && v.LastTemperature is { } t
                             && ((active.TemperatureMin is { } min && t < min) || (active.TemperatureMax is { } max && t > max));
            return new MapVehicle(
                v.Id, v.Plate, v.CarrierId is { } c && carriers.TryGetValue(c, out var name) ? name : "Proseware",
                v.LastPosition?.Latitude(), v.LastPosition?.Longitude(), v.LastPositionAt, v.LastSpeedKmh, v.LastTemperature,
                v.HasReefer, v.LastPositionAt is null || v.LastPositionAt < now - StaleAfter, active?.Number, outOfRange);
        }).ToList();

        var locations = await _db.Locations.AsNoTracking()
            .Where(l => l.Kind != LocationKind.CustomerSite)
            .ToListAsync(ct);

        return new MapData(
            mapVehicles,
            legs,
            locations.Select(l => new MapLocation(l.Id, l.Code, l.Name, l.Kind, l.Position.Latitude(), l.Position.Longitude())).ToList());
    }
}

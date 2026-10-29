using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Tower.Models;

namespace Proseware.ControlTower.Tower.Services;

/// <summary>Expected inbound and outbound legs per terminal and hour, for dock and staff planning.</summary>
public sealed class TerminalWorkloadService
{
    private readonly ControlTowerDbContext _db;
    private readonly IClock _clock;

    public TerminalWorkloadService(ControlTowerDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<TerminalWorkload>> GetAsync(int hours, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var start = new DateTimeOffset(now.UtcDateTime.Date.AddHours(now.UtcDateTime.Hour), TimeSpan.Zero);
        var end = start.AddHours(Math.Clamp(hours, 1, 168));

        var terminals = await _db.Locations.AsNoTracking().Where(l => l.Kind == LocationKind.Terminal).OrderBy(l => l.Code).ToListAsync(ct);
        var ids = terminals.Select(t => t.Id).ToList();

        var legs = await _db.Legs.AsNoTracking()
            .Where(l => l.Status != LegStatus.Cancelled && (ids.Contains(l.FromId) || ids.Contains(l.ToId)))
            .Where(l => (l.ActualArrival == null && l.PlannedArrival < end) || (l.ActualDeparture == null && l.PlannedDeparture < end))
            .Select(l => new { l.ShipmentId, l.FromId, l.ToId, l.PlannedArrival, l.PlannedDeparture, l.Eta, l.ActualArrival, l.ActualDeparture, l.Status })
            .ToListAsync(ct);

        var shipmentIds = legs.Select(l => l.ShipmentId).Distinct().ToList();
        var packages = await _db.Packages.AsNoTracking()
            .Where(p => shipmentIds.Contains(p.ShipmentId))
            .Select(p => new { p.ShipmentId, p.Quantity, p.WeightKg })
            .ToListAsync(ct);
        var packageMap = packages.GroupBy(p => p.ShipmentId)
            .ToDictionary(g => g.Key, g => (Count: g.Sum(p => p.Quantity), Weight: g.Sum(p => p.WeightKg * p.Quantity)));

        DateTimeOffset Bucket(DateTimeOffset t)
        {
            var b = new DateTimeOffset(t.UtcDateTime.Date.AddHours(t.UtcDateTime.Hour), TimeSpan.Zero);
            return b < start ? start : b; // overdue work counts in the current hour
        }

        var result = new List<TerminalWorkload>();
        foreach (var t in terminals)
        {
            var arrivals = legs.Where(l => l.ToId == t.Id && l.ActualArrival == null)
                .Select(l => (Hour: Bucket(l.Eta ?? l.PlannedArrival), l.ShipmentId)).Where(x => x.Hour < end).ToList();
            var departures = legs.Where(l => l.FromId == t.Id && l.ActualDeparture == null)
                .Select(l => (Hour: Bucket(l.PlannedDeparture), l.ShipmentId)).Where(x => x.Hour < end).ToList();
            var onSite = legs.Count(l => l.FromId == t.Id && l.ActualDeparture == null && l.PlannedDeparture < now.AddHours(12)
                                         && legs.Any(i => i.ShipmentId == l.ShipmentId && i.ToId == t.Id && i.ActualArrival != null));

            var hoursList = new List<TerminalHour>();
            for (var h = start; h < end; h = h.AddHours(1))
            {
                var a = arrivals.Where(x => x.Hour == h).ToList();
                var d = departures.Where(x => x.Hour == h).ToList();
                var shipments = a.Concat(d).Select(x => x.ShipmentId).Distinct().ToList();
                hoursList.Add(new TerminalHour(h, a.Count, d.Count,
                    shipments.Sum(id => packageMap.TryGetValue(id, out var p) ? p.Count : 0),
                    shipments.Sum(id => packageMap.TryGetValue(id, out var p) ? p.Weight : 0m)));
            }
            result.Add(new TerminalWorkload(t.Id, t.Code, t.Name, t.TimeZoneId, hoursList, onSite));
        }
        return result;
    }
}

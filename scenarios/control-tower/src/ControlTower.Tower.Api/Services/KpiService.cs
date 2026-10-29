using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Kpi;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Tower.Models;

namespace Proseware.ControlTower.Tower.Services;

public enum KpiGrouping
{
    Total,
    Mode,
    Customer,
    Carrier
}

public sealed class KpiService
{
    private readonly ControlTowerDbContext _db;
    private readonly IClock _clock;

    public KpiService(ControlTowerDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>Daily KPI series from the aggregates written by the jobs service.</summary>
    public async Task<IReadOnlyList<KpiPoint>> SeriesAsync(DateTime from, DateTime to, KpiGrouping groupBy, int? customerId, CancellationToken ct)
    {
        var q = _db.DailyKpis.AsNoTracking().Where(k => k.Day >= from.Date && k.Day <= to.Date);
        if (customerId is { } cid) q = q.Where(k => k.CustomerId == cid);
        var rows = await q.ToListAsync(ct);

        var customers = await _db.Customers.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var carriers = await _db.Carriers.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        string GroupName(DailyKpi k) => groupBy switch
        {
            KpiGrouping.Mode => k.Mode.ToString(),
            KpiGrouping.Customer => k.CustomerId is { } c && customers.TryGetValue(c, out var n) ? n : "(none)",
            KpiGrouping.Carrier => k.CarrierId is { } c && carriers.TryGetValue(c, out var n) ? n : "(all carriers)",
            _ => "Total"
        };

        return rows
            .GroupBy(k => (k.Day, Group: GroupName(k)))
            .OrderBy(g => g.Key.Day).ThenBy(g => g.Key.Group)
            .Select(g =>
            {
                var sum = KpiCalculator.Sum(g);
                return new KpiPoint(g.Key.Day, g.Key.Group, sum.ShipmentsBooked, sum.ShipmentsDelivered, sum.OnTimeDeliveryPercent,
                    sum.OnTimePickupPercent, sum.AlertsRaised, Math.Round(sum.AverageTransitHours, 1), sum.Revenue);
            })
            .ToList();
    }

    /// <summary>Carrier on-time performance per leg arrival over the period.</summary>
    public async Task<IReadOnlyList<CarrierScore>> CarrierScorecardAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var carriers = await _db.Carriers.AsNoTracking().Where(c => c.Active).ToListAsync(ct);
        var legs = await _db.Legs.AsNoTracking()
            .Where(l => l.CarrierId != null && l.Status != LegStatus.Cancelled && l.PlannedArrival >= from && l.PlannedArrival < to)
            .Select(l => new { l.CarrierId, l.Status, l.PlannedArrival, l.ActualArrival, l.Eta })
            .ToListAsync(ct);

        var grace = TimeSpan.FromHours(1);
        return carriers.Select(c =>
        {
            var mine = legs.Where(l => l.CarrierId == c.Id).ToList();
            var arrived = mine.Where(l => l.ActualArrival != null).ToList();
            var onTime = arrived.Count(l => l.ActualArrival <= l.PlannedArrival + grace);
            var open = mine.Where(l => l.ActualArrival == null).ToList();
            return new CarrierScore(
                c.Id, c.Code, c.Name, c.Mode, c.Integration,
                mine.Count, arrived.Count, onTime,
                arrived.Count == 0 ? 0 : Math.Round(100.0 * onTime / arrived.Count, 1),
                c.OnTimeTargetPercent,
                arrived.Count == 0 ? 0 : Math.Round(arrived.Average(l => Math.Max(0, (l.ActualArrival!.Value - l.PlannedArrival).TotalHours)), 1),
                open.Count,
                open.Count(l => (l.Eta ?? (l.PlannedArrival < now ? now : l.PlannedArrival)) > l.PlannedArrival + grace));
        })
        .OrderBy(s => s.OnTimePercent - (double)s.TargetPercent)
        .ToList();
    }
}

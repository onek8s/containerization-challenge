using Hangfire;
using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Kpi;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Jobs.Jobs;

/// <summary>Recalculates the daily KPI rows for yesterday and today.</summary>
public sealed class KpiAggregationJob
{
    private readonly ControlTowerDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<KpiAggregationJob> _logger;

    public KpiAggregationJob(ControlTowerDbContext db, IClock clock, ILogger<KpiAggregationJob> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    [DisableConcurrentExecution(600)]
    [AutomaticRetry(Attempts = 2)]
    public async Task RunAsync(CancellationToken ct)
    {
        var today = _clock.UtcNow.UtcDateTime.Date;
        await RecalculateAsync(today.AddDays(-1), ct);
        await RecalculateAsync(today, ct);
    }

    /// <summary>Recalculates one day; used from the dashboard to rebuild history.</summary>
    [DisableConcurrentExecution(600)]
    public async Task RecalculateAsync(DateTime day, CancellationToken ct)
    {
        var start = new DateTimeOffset(day.Date, TimeSpan.Zero);
        var end = start.AddDays(1);

        var shipments = await _db.Shipments.AsNoTracking()
            .Include(s => s.Destination)
            .Include(s => s.Packages)
            .Include(s => s.Charges)
            .Where(s => (s.CreatedAt >= start && s.CreatedAt < end)
                        || (s.ActualDelivery >= start && s.ActualDelivery < end)
                        || (s.PlannedPickup >= start && s.PlannedPickup < end))
            .AsSplitQuery()
            .ToListAsync(ct);
        var ids = shipments.Select(s => s.Id).ToList();
        var alerts = await _db.Alerts.AsNoTracking()
            .Where(a => a.RaisedAt >= start && a.RaisedAt < end && ids.Contains(a.ShipmentId))
            .ToListAsync(ct);

        var rows = new KpiCalculator().Calculate(day.Date, shipments, alerts, _clock.UtcNow);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var existing = await _db.DailyKpis.Where(k => k.Day == day.Date).ToListAsync(ct);
        _db.DailyKpis.RemoveRange(existing);
        _db.DailyKpis.AddRange(rows);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation("KPI {Day:yyyy-MM-dd}: {Rows} rows from {Shipments} shipments ({Delivered} delivered)",
            day, rows.Count, shipments.Count, shipments.Count(s => s.Status == ShipmentStatus.Delivered));
    }
}

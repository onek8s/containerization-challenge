using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Domain.Tracking;
using Proseware.ControlTower.Integration.Messaging;
using Proseware.ControlTower.Tower.Models;

namespace Proseware.ControlTower.Tower.Services;

public sealed class AlertService
{
    private readonly ControlTowerDbContext _db;
    private readonly IEventBus _bus;
    private readonly IClock _clock;
    private readonly ILogger<AlertService> _logger;

    public AlertService(ControlTowerDbContext db, IEventBus bus, IClock clock, ILogger<AlertService> logger)
    {
        _db = db;
        _bus = bus;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AlertRow>> ListAsync(bool openOnly, Severity? minSeverity, int? shipmentId, int take, CancellationToken ct)
    {
        var q = _db.Alerts.AsNoTracking().AsQueryable();
        if (openOnly) q = q.Where(a => a.ResolvedAt == null);
        if (minSeverity is { } sev) q = q.Where(a => a.Severity >= sev);
        if (shipmentId is { } id) q = q.Where(a => a.ShipmentId == id);

        var rows = await q
            .Join(_db.Shipments, a => a.ShipmentId, s => s.Id, (a, s) => new { a, s.Number, Customer = s.Customer!.Name })
            .ToListAsync(ct);

        return rows
            .OrderByDescending(r => r.a.ResolvedAt is null)
            .ThenByDescending(r => r.a.Severity)
            .ThenByDescending(r => r.a.RaisedAt)
            .Take(Math.Clamp(take, 1, 1000))
            .Select(r => ToRow(r.a, r.Number, r.Customer))
            .ToList();
    }

    public async Task<AlertRow?> AcknowledgeAsync(long id, string user, CancellationToken ct)
    {
        var alert = await _db.Alerts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (alert is null) return null;
        if (alert.AcknowledgedAt is null)
        {
            alert.AcknowledgedAt = _clock.UtcNow;
            alert.AcknowledgedBy = user;
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Alert {AlertId} acknowledged by {User}", id, user);
        }
        return await GetRowAsync(alert, ct);
    }

    public async Task<AlertRow?> ResolveAsync(long id, string user, string resolution, CancellationToken ct)
    {
        var alert = await _db.Alerts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (alert is null) return null;
        if (alert.ResolvedAt is not null) return await GetRowAsync(alert, ct);

        var now = _clock.UtcNow;
        alert.AcknowledgedAt ??= now;
        alert.AcknowledgedBy ??= user;
        alert.ResolvedAt = now;
        alert.Resolution = string.IsNullOrWhiteSpace(resolution) ? "Resolved" : resolution.Trim();
        await _db.SaveChangesAsync(ct);

        await _bus.PublishAsync(new AlertClosed(IntegrationEvent.NewId(), now, alert.Id, alert.ShipmentId, user, alert.Resolution), ct);
        _logger.LogInformation("Alert {AlertId} ({Rule}) resolved by {User}", id, alert.RuleCode, user);
        return await GetRowAsync(alert, ct);
    }

    private async Task<AlertRow> GetRowAsync(ShipmentAlert alert, CancellationToken ct)
    {
        var s = await _db.Shipments.AsNoTracking().Where(x => x.Id == alert.ShipmentId)
            .Select(x => new { x.Number, Customer = x.Customer!.Name }).FirstAsync(ct);
        return ToRow(alert, s.Number, s.Customer);
    }

    internal static AlertRow ToRow(ShipmentAlert a, string number, string customer) => new(
        a.Id, a.ShipmentId, number, customer, a.RuleCode, a.Severity, a.Message, a.RaisedAt, a.LastSeenAt, a.Occurrences,
        a.AcknowledgedBy, a.AcknowledgedAt, a.ResolvedAt, a.Resolution);
}

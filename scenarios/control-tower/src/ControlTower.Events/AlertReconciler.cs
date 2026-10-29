using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Domain.Tracking;
using Proseware.ControlTower.Integration.Messaging;
using Proseware.ControlTower.Rules;

namespace Proseware.ControlTower.Events;

/// <summary>Evaluates the rules and makes the open alerts match the result.</summary>
public sealed class AlertReconciler
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly ControlTowerDbContext _db;
    private readonly ShipmentQueries _queries;
    private readonly AlertEvaluator _evaluator;
    private readonly VehicleMotionState _motion;
    private readonly IEventBus _bus;
    private readonly IClock _clock;
    private readonly ILogger<AlertReconciler> _logger;

    public AlertReconciler(ControlTowerDbContext db, ShipmentQueries queries, AlertEvaluator evaluator, VehicleMotionState motion, IEventBus bus, IClock clock, ILogger<AlertReconciler> logger)
    {
        _db = db;
        _queries = queries;
        _evaluator = evaluator;
        _motion = motion;
        _bus = bus;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Evaluates all open shipments, or only the given ones.</summary>
    public async Task<(int Raised, int Closed)> RunAsync(IReadOnlyCollection<int>? shipmentIds, int scanLookbackHours, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var now = _clock.UtcNow;
            var query = _queries.Load().Where(s => s.Status != ShipmentStatus.Delivered && s.Status != ShipmentStatus.Cancelled && s.Status != ShipmentStatus.Requested);
            if (shipmentIds is not null) query = query.Where(s => shipmentIds.Contains(s.Id));
            var shipments = await query.ToListAsync(ct);
            var ids = shipments.Select(s => s.Id).ToList();

            var vehicleIds = shipments.SelectMany(s => s.Legs).Where(l => l.VehicleId != null).Select(l => l.VehicleId!.Value).Distinct().ToList();
            var vehicles = await _db.Vehicles.AsNoTracking().Where(v => vehicleIds.Contains(v.Id)).ToListAsync(ct);
            var locations = await _db.Locations.AsNoTracking().ToListAsync(ct);
            var since = now.AddHours(-scanLookbackHours);
            var scans = await _db.Scans.AsNoTracking().Where(x => x.ShipmentId != null && ids.Contains(x.ShipmentId.Value) && x.ScannedAt >= since).ToListAsync(ct);

            var facts = AlertFactsBuilder.Build(now, shipments, vehicles, _motion.Snapshot(), locations, scans);
            var candidates = _evaluator.Evaluate(facts);

            var openQuery = _db.Alerts.Where(a => a.ResolvedAt == null);
            if (shipmentIds is not null) openQuery = openQuery.Where(a => shipmentIds.Contains(a.ShipmentId));
            var open = await openQuery.ToListAsync(ct);
            var numbers = shipments.ToDictionary(s => s.Id, s => s.Number);

            var raised = new List<ShipmentAlert>();
            foreach (var c in candidates)
            {
                var existing = open.FirstOrDefault(a => a.ShipmentId == c.ShipmentId && a.RuleCode == c.RuleCode);
                if (existing is null)
                {
                    var alert = new ShipmentAlert
                    {
                        ShipmentId = c.ShipmentId,
                        RuleCode = c.RuleCode,
                        Severity = c.Severity,
                        Message = c.Message.Length > 400 ? c.Message[..400] : c.Message,
                        RaisedAt = now,
                        LastSeenAt = now
                    };
                    _db.Alerts.Add(alert);
                    raised.Add(alert);
                }
                else
                {
                    existing.LastSeenAt = now;
                    existing.Occurrences++;
                    existing.Message = c.Message.Length > 400 ? c.Message[..400] : c.Message;
                    if (c.Severity > existing.Severity)
                    {
                        existing.Severity = c.Severity;
                        // escalation is announced like a new alert
                        raised.Add(existing);
                    }
                }
            }

            // only shipments that were evaluated can have their alerts cleared; alerts of
            // delivered or cancelled shipments are closed as well
            var closed = open.Where(a => !candidates.Any(c => c.ShipmentId == a.ShipmentId && c.RuleCode == a.RuleCode)).ToList();
            foreach (var a in closed)
            {
                a.ResolvedAt = now;
                a.Resolution = ids.Contains(a.ShipmentId) ? "Condition cleared" : "Shipment closed";
            }

            await _db.SaveChangesAsync(ct);

            foreach (var a in raised)
            {
                numbers.TryGetValue(a.ShipmentId, out var number);
                await _bus.PublishAsync(new AlertRaised(IntegrationEvent.NewId(), now, a.Id, a.ShipmentId, number ?? "", a.RuleCode, a.Severity, a.Message), ct);
            }
            foreach (var a in closed)
                await _bus.PublishAsync(new AlertClosed(IntegrationEvent.NewId(), now, a.Id, a.ShipmentId, "rules", a.Resolution!), ct);

            if (raised.Count + closed.Count > 0)
                _logger.LogInformation("Rules: {Raised} alerts raised or escalated, {Closed} closed ({Shipments} shipments)", raised.Count, closed.Count, shipments.Count);
            return (raised.Count, closed.Count);
        }
        finally
        {
            Gate.Release();
        }
    }
}

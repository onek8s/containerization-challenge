using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Domain.Scanning;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Integration.Messaging;

namespace Proseware.ControlTower.Events;

/// <summary>Message handlers of the event processor. Each runs in its own DI scope.</summary>
public sealed class EventSubscriptionsService : BackgroundService
{
    private readonly IEventBus _bus;
    private readonly IServiceScopeFactory _scopes;
    private readonly VehicleMotionState _motion;
    private readonly EventsOptions _options;
    private readonly ILogger<EventSubscriptionsService> _logger;
    private readonly List<IDisposable> _subscriptions = new();

    public EventSubscriptionsService(IEventBus bus, IServiceScopeFactory scopes, VehicleMotionState motion, IOptions<EventsOptions> options, ILogger<EventSubscriptionsService> logger)
    {
        _bus = bus;
        _scopes = scopes;
        _motion = motion;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _subscriptions.Add(await _bus.SubscribeAsync<StatusReported>("events-status", OnStatusAsync, stoppingToken));
        _subscriptions.Add(await _bus.SubscribeAsync<ScanRecorded>("events-scans", OnScanAsync, stoppingToken));
        _subscriptions.Add(await _bus.SubscribeAsync<GeofenceEvent>("events-geofence", OnGeofenceAsync, stoppingToken));
        _subscriptions.Add(await _bus.SubscribeAsync<VehiclePositionReported>("events-positions", OnPositionAsync, stoppingToken));
        _subscriptions.Add(await _bus.SubscribeAsync<CustomsStatusChanged>("events-customs", OnCustomsAsync, stoppingToken));
        _subscriptions.Add(await _bus.SubscribeAsync<ShipmentBooked>("events-booked", (m, ct) => EvaluateAsync(new[] { m.ShipmentId }, ct), stoppingToken));
        _logger.LogInformation("Event processor subscribed to {Count} message types", _subscriptions.Count);
        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var s in _subscriptions) s.Dispose();
        return base.StopAsync(cancellationToken);
    }

    internal async Task OnStatusAsync(StatusReported status, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<MilestoneProcessor>();
        await processor.ProcessAsync(status, ct);
    }

    internal async Task OnScanAsync(ScanRecorded scan, CancellationToken ct)
    {
        var milestone = scan.Kind switch
        {
            ScanKind.Receive or ScanKind.Unload => MilestoneCodes.ArrivedAtTerminal,
            ScanKind.Load => MilestoneCodes.Loaded,
            ScanKind.Deliver => MilestoneCodes.Delivered,
            ScanKind.Damage => MilestoneCodes.Damaged,
            _ => null
        };
        if (milestone is null) return;

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ControlTowerDbContext>();
        var terminal = await db.Locations.AsNoTracking().Where(l => l.Id == scan.TerminalId).Select(l => new { l.Code, l.City }).FirstOrDefaultAsync(ct);

        // a shipment counts as arrived / loaded when its first package is scanned
        if (scan.ShipmentId is { } shipmentId && milestone is MilestoneCodes.ArrivedAtTerminal or MilestoneCodes.Loaded)
        {
            var since = scan.OccurredAt.AddHours(-12);
            var earlier = await db.Scans.AnyAsync(s => s.ShipmentId == shipmentId && s.Kind == scan.Kind && s.TerminalId == scan.TerminalId && s.ScannedAt >= since && s.Id != scan.ScanId, ct);
            if (earlier) return;
        }

        await OnStatusAsync(new StatusReported(IntegrationEvent.NewId(), scan.OccurredAt, null, null, scan.Sscc, milestone, terminal?.Code, terminal?.City,
            "scan:" + scan.DeviceId, $"{scan.Kind} scan of {scan.Sscc}"), ct);
    }

    internal async Task OnGeofenceAsync(GeofenceEvent e, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ControlTowerDbContext>();
        var legs = await db.Legs.AsNoTracking()
            .Where(l => l.VehicleId == e.VehicleId
                        && ((e.Entered && l.ToId == e.LocationId && l.Status == LegStatus.Departed)
                            || (!e.Entered && l.FromId == e.LocationId && (l.Status == LegStatus.Planned || l.Status == LegStatus.Confirmed))))
            .Join(db.Shipments, l => l.ShipmentId, s => s.Id, (l, s) => new { l.Sequence, l.ToId, s.Number, s.Status, ToKind = l.To!.Kind, LegCount = s.Legs.Count })
            .ToListAsync(ct);

        foreach (var leg in legs)
        {
            if (leg.Status is ShipmentStatus.Delivered or ShipmentStatus.Cancelled) continue;
            string code;
            if (e.Entered)
                code = leg.ToKind == Domain.LocationKind.Terminal ? MilestoneCodes.ArrivedAtTerminal : MilestoneCodes.Arrived;
            else
                code = leg.Sequence == 1 ? MilestoneCodes.PickedUp : MilestoneCodes.DepartedTerminal;
            await OnStatusAsync(new StatusReported(IntegrationEvent.NewId(), e.OccurredAt, leg.Number, null, null, code, e.LocationCode, null,
                "telematics:" + e.Plate, e.Entered ? $"{e.Plate} entered {e.LocationCode}" : $"{e.Plate} left {e.LocationCode}"), ct);
        }
    }

    internal async Task OnPositionAsync(VehiclePositionReported position, CancellationToken ct)
    {
        _motion.Update(position.VehicleId, position.SpeedKmh, position.OccurredAt, _options.StoppedBelowKmh);
        using var scope = _scopes.CreateScope();
        var eta = scope.ServiceProvider.GetRequiredService<EtaService>();
        var shipmentIds = await eta.UpdateAsync(position, ct);
        // cold chain and door alarms should not wait for the sweep
        if (shipmentIds.Count > 0 && (position.Temperature is not null || position.DoorOpen == true))
            await EvaluateAsync(shipmentIds, ct);
    }

    internal Task OnCustomsAsync(CustomsStatusChanged change, CancellationToken ct) => EvaluateAsync(new[] { change.ShipmentId }, ct);

    private async Task EvaluateAsync(IReadOnlyCollection<int> shipmentIds, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var reconciler = scope.ServiceProvider.GetRequiredService<AlertReconciler>();
        await reconciler.RunAsync(shipmentIds, _options.ScanLookbackHours, ct);
    }
}

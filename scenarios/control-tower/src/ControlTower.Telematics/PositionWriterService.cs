using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Domain.Fleet;
using Proseware.ControlTower.Domain.Locations;
using Proseware.ControlTower.Integration.Messaging;

namespace Proseware.ControlTower.Telematics;

/// <summary>Writes readings in batches, keeps vehicles' last known state and raises geofence events.</summary>
public sealed class PositionWriterService : BackgroundService
{
    private readonly ChannelReader<TelematicsReading> _reader;
    private readonly IServiceScopeFactory _scopes;
    private readonly IEventBus _bus;
    private readonly TelematicsOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<PositionWriterService> _logger;
    private readonly GeofenceTracker _geofences;
    private readonly Dictionary<string, (int Id, string Plate)> _vehiclesByUnit = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, ReeferReading> _lastReefer = new();
    private DateTimeOffset _cacheLoadedAt = DateTimeOffset.MinValue;

    public PositionWriterService(Channel<TelematicsReading> channel, IServiceScopeFactory scopes, IEventBus bus, IOptions<TelematicsOptions> options,
        IClock clock, ILogger<PositionWriterService> logger)
    {
        _reader = channel.Reader;
        _scopes = scopes;
        _bus = bus;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
        _geofences = new GeofenceTracker(_options.GeofenceExitMarginMeters);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<TelematicsReading>(_options.BatchSize);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var window = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                window.CancelAfter(TimeSpan.FromSeconds(_options.BatchSeconds));
                try
                {
                    while (batch.Count < _options.BatchSize && await _reader.WaitToReadAsync(window.Token))
                        while (batch.Count < _options.BatchSize && _reader.TryRead(out var reading))
                            batch.Add(reading);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // batch window elapsed
                }

                if (batch.Count > 0)
                {
                    await WriteBatchAsync(batch, stoppingToken);
                    batch.Clear();
                }
                else if (_reader.Completion.IsCompleted)
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Writing {Count} telematics readings failed; batch dropped", batch.Count);
                batch.Clear();
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    internal async Task WriteBatchAsync(IReadOnlyList<TelematicsReading> batch, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ControlTowerDbContext>();
        await RefreshCacheAsync(db, ct);

        var now = _clock.UtcNow;
        var live = now.AddMinutes(-_options.LiveWindowMinutes);
        var events = new List<IIntegrationEvent>();
        var touched = new Dictionary<int, Vehicle>();

        async Task<Vehicle?> VehicleAsync(int id)
        {
            if (!touched.TryGetValue(id, out var v))
            {
                v = await db.Vehicles.FindAsync(new object[] { id }, ct);
                if (v is not null) touched[id] = v;
            }
            return v;
        }

        foreach (var reading in batch.OrderBy(r => r.Timestamp))
        {
            if (!_vehiclesByUnit.TryGetValue(reading.UnitId, out var known))
            {
                _logger.LogDebug("Unknown telematics unit {Unit}", reading.UnitId);
                continue;
            }
            var vehicle = await VehicleAsync(known.Id);
            if (vehicle is null) continue;

            switch (reading)
            {
                case ReeferReading reefer:
                    _lastReefer[vehicle.Id] = reefer;
                    if (reefer.Temperature is { } t) vehicle.LastTemperature = t;
                    break;

                case PositionReading p:
                    _lastReefer.TryGetValue(vehicle.Id, out var lastReefer);
                    var fresh = lastReefer is not null && (p.Timestamp - lastReefer.Timestamp).Duration() < TimeSpan.FromMinutes(10);
                    var point = GeoPoints.Create(p.Latitude, p.Longitude);
                    db.VehiclePositions.Add(new VehiclePosition
                    {
                        VehicleId = vehicle.Id,
                        RecordedAt = p.Timestamp,
                        Position = point,
                        SpeedKmh = p.SpeedKmh,
                        HeadingDegrees = p.Heading,
                        IgnitionOn = p.Ignition,
                        ReeferTemperature = fresh ? lastReefer!.Temperature : null,
                        ReeferSetpoint = fresh ? lastReefer!.Setpoint : null,
                        DoorOpen = fresh ? lastReefer!.DoorOpen : null
                    });

                    if (vehicle.LastPositionAt is null || p.Timestamp > vehicle.LastPositionAt)
                    {
                        vehicle.LastPositionAt = p.Timestamp;
                        vehicle.LastPosition = point;
                        vehicle.LastSpeedKmh = p.SpeedKmh;
                    }

                    foreach (var transition in _geofences.Update(vehicle.Id, p.Latitude, p.Longitude))
                    {
                        events.Add(new GeofenceEvent(IntegrationEvent.NewId(), p.Timestamp, vehicle.Id, vehicle.Plate, transition.Location.Id, transition.Location.Code, transition.Entered));
                    }

                    if (p.Timestamp >= live)
                    {
                        events.Add(new VehiclePositionReported(IntegrationEvent.NewId(), p.Timestamp, vehicle.Id, vehicle.Plate, p.Latitude, p.Longitude, p.SpeedKmh, p.Heading,
                            fresh ? lastReefer!.Temperature : null, fresh ? lastReefer!.DoorOpen : null));
                    }
                    break;
            }
        }

        await db.SaveChangesAsync(ct);
        foreach (var e in events) await PublishAsync(e, ct);
        if (batch.Count >= _options.BatchSize)
            _logger.LogDebug("Wrote a full batch of {Count} readings", batch.Count);
    }

    private Task PublishAsync(IIntegrationEvent e, CancellationToken ct) => e switch
    {
        GeofenceEvent g => _bus.PublishAsync(g, ct),
        VehiclePositionReported v => _bus.PublishAsync(v, ct),
        _ => Task.CompletedTask
    };

    private async Task RefreshCacheAsync(ControlTowerDbContext db, CancellationToken ct)
    {
        if (_clock.UtcNow - _cacheLoadedAt < TimeSpan.FromMinutes(_options.VehicleCacheMinutes)) return;
        var vehicles = await db.Vehicles.AsNoTracking().Where(v => v.TelematicsUnitId != null).Select(v => new { v.Id, v.Plate, v.TelematicsUnitId }).ToListAsync(ct);
        _vehiclesByUnit.Clear();
        foreach (var v in vehicles) _vehiclesByUnit[v.TelematicsUnitId!] = (v.Id, v.Plate);
        _geofences.SetLocations(await db.Locations.AsNoTracking().ToListAsync(ct));
        _cacheLoadedAt = _clock.UtcNow;
        _logger.LogInformation("Telematics cache loaded: {Vehicles} vehicles", vehicles.Count);
    }
}

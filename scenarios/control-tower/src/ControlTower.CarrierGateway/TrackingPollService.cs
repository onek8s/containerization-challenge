using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Integration.Carriers;
using Proseware.ControlTower.Integration.Messaging;

namespace Proseware.ControlTower.CarrierGateway;

/// <summary>Pulls tracking events from carriers with a REST API for legs that are booked and not yet arrived.</summary>
public sealed class TrackingPollService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ICarrierApiClientResolver _clients;
    private readonly IEventBus _bus;
    private readonly GatewayOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<TrackingPollService> _logger;
    private readonly Dictionary<int, DateTimeOffset> _lastPoll = new();

    public TrackingPollService(IServiceScopeFactory scopes, ICarrierApiClientResolver clients, IEventBus bus, IOptions<GatewayOptions> options, IClock clock, ILogger<TrackingPollService> logger)
    {
        _scopes = scopes;
        _clients = clients;
        _bus = bus;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.TrackingEnabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.TrackingPollSeconds));
        do
        {
            try
            {
                await PollAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Tracking poll failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PollAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ControlTowerDbContext>();
        var carriers = await db.Carriers.AsNoTracking().Where(c => c.Active && c.Integration == CarrierIntegration.RestApi).ToListAsync(ct);
        foreach (var carrier in carriers)
        {
            var client = _clients.Resolve(carrier);
            if (client is null) continue;

            var legs = await db.Legs.AsNoTracking()
                .Where(l => l.CarrierId == carrier.Id
                            && l.CarrierReference != null
                            && (l.Status == LegStatus.Confirmed || l.Status == LegStatus.Departed))
                .Join(db.Shipments, l => l.ShipmentId, s => s.Id, (l, s) => new TrackingQuery(s.Id, s.Number, l.CarrierReference!))
                .ToListAsync(ct);
            if (legs.Count == 0) continue;

            var now = _clock.UtcNow;
            var since = _lastPoll.TryGetValue(carrier.Id, out var last) ? last.AddMinutes(-10) : now.AddDays(-1);
            try
            {
                var events = await client.GetTrackingAsync(carrier, legs, since, ct);
                foreach (var e in events) await _bus.PublishAsync(e, ct);
                _lastPoll[carrier.Id] = now;
                if (events.Count > 0)
                    _logger.LogInformation("{Count} tracking events from {Carrier} for {Legs} legs", events.Count, carrier.Code, legs.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tracking poll of {Carrier} failed", carrier.Code);
            }
        }
    }
}

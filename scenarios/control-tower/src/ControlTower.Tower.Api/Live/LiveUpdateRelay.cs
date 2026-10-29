using Microsoft.AspNetCore.SignalR;
using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Integration.Messaging;
using Proseware.ControlTower.Tower.Hubs;

namespace Proseware.ControlTower.Tower.Live;

/// <summary>
/// Forwards broker events to the browsers connected to this API.
/// Vehicle positions are throttled per vehicle to keep the map traffic reasonable.
/// </summary>
public sealed class LiveUpdateRelay : BackgroundService
{
    private static readonly TimeSpan PositionInterval = TimeSpan.FromSeconds(10);

    private readonly IEventBus _bus;
    private readonly IHubContext<TowerHub, ITowerClient> _hub;
    private readonly ILogger<LiveUpdateRelay> _logger;
    private readonly Dictionary<int, DateTimeOffset> _lastPosition = new();
    private readonly List<IDisposable> _subscriptions = new();

    public LiveUpdateRelay(IEventBus bus, IHubContext<TowerHub, ITowerClient> hub, ILogger<LiveUpdateRelay> logger)
    {
        _bus = bus;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var attempt = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _subscriptions.Add(await _bus.SubscribeAsync<MilestoneRecorded>("tower-live-milestone", OnMilestone, stoppingToken));
                _subscriptions.Add(await _bus.SubscribeAsync<AlertRaised>("tower-live-alert-raised", (m, _) => _hub.Clients.All.AlertRaised(m), stoppingToken));
                _subscriptions.Add(await _bus.SubscribeAsync<AlertClosed>("tower-live-alert-closed", (m, _) => _hub.Clients.All.AlertClosed(m), stoppingToken));
                _subscriptions.Add(await _bus.SubscribeAsync<EtaUpdated>("tower-live-eta", OnEta, stoppingToken));
                _subscriptions.Add(await _bus.SubscribeAsync<VehiclePositionReported>("tower-live-position", OnPosition, stoppingToken));
                _logger.LogInformation("Live update relay subscribed");
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                foreach (var s in _subscriptions) s.Dispose();
                _subscriptions.Clear();
                var delay = TimeSpan.FromSeconds(Math.Min(60, 5 * ++attempt));
                _logger.LogWarning(ex, "Could not subscribe to live updates, retrying in {Delay}", delay);
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    private async Task OnMilestone(MilestoneRecorded m, CancellationToken ct)
    {
        await _hub.Clients.All.Milestone(m);
    }

    private async Task OnEta(EtaUpdated m, CancellationToken ct)
    {
        await _hub.Clients.All.EtaUpdated(m);
    }

    private Task OnPosition(VehiclePositionReported m, CancellationToken ct)
    {
        lock (_lastPosition)
        {
            if (_lastPosition.TryGetValue(m.VehicleId, out var last) && m.OccurredAt - last < PositionInterval)
                return Task.CompletedTask;
            _lastPosition[m.VehicleId] = m.OccurredAt;
        }
        return _hub.Clients.All.VehiclePosition(m);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var s in _subscriptions) s.Dispose();
        _subscriptions.Clear();
        return base.StopAsync(cancellationToken);
    }
}

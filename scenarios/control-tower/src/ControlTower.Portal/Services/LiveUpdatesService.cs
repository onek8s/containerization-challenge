using Microsoft.AspNetCore.SignalR;
using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Integration.Messaging;
using Proseware.ControlTower.Portal.Hubs;

namespace Proseware.ControlTower.Portal.Services;

/// <summary>Forwards shipment events from the bus to the browsers of the customer's users.</summary>
public sealed class LiveUpdatesService : BackgroundService
{
    private readonly IEventBus _bus;
    private readonly IHubContext<TrackingHub> _hub;
    private readonly ILogger<LiveUpdatesService> _logger;
    private readonly List<IDisposable> _subscriptions = new();

    public LiveUpdatesService(IEventBus bus, IHubContext<TrackingHub> hub, ILogger<LiveUpdatesService> logger)
    {
        _bus = bus;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _subscriptions.Add(await _bus.SubscribeAsync<MilestoneRecorded>("portal-live-milestones", OnMilestoneAsync, stoppingToken));
        _subscriptions.Add(await _bus.SubscribeAsync<EtaUpdated>("portal-live-eta", OnEtaAsync, stoppingToken));
        _logger.LogInformation("Live updates subscribed");
    }

    private Task OnMilestoneAsync(MilestoneRecorded m, CancellationToken ct) =>
        _hub.Clients.Group(CustomerContext.GroupName(m.CustomerId)).SendAsync("milestone",
            new MilestoneUpdate(m.ShipmentId, m.Number, m.MilestoneCode, m.Description, m.Location, m.Status.ToString(), m.OccurredAt), ct);

    private Task OnEtaAsync(EtaUpdated e, CancellationToken ct) =>
        _hub.Clients.Group(CustomerContext.GroupName(e.CustomerId)).SendAsync("eta",
            new EtaUpdate(e.ShipmentId, e.Number, e.Eta, Math.Round(e.RemainingKm)), ct);

    public override void Dispose()
    {
        foreach (var s in _subscriptions) s.Dispose();
        base.Dispose();
    }
}

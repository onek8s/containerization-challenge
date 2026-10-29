using Microsoft.AspNetCore.SignalR;

namespace Proseware.ControlTower.Tower.Hubs;

/// <summary>
/// Live updates for the operations desk. Clients may join a terminal group to receive
/// only the events of their own terminal in addition to the global stream.
/// </summary>
public sealed class TowerHub : Hub<ITowerClient>
{
    private readonly ILogger<TowerHub> _logger;

    public TowerHub(ILogger<TowerHub> logger) => _logger = logger;

    public override Task OnConnectedAsync()
    {
        _logger.LogDebug("Tower client {ConnectionId} connected as {User}", Context.ConnectionId, Context.User?.Identity?.Name);
        return base.OnConnectedAsync();
    }

    public Task WatchShipment(int shipmentId) => Groups.AddToGroupAsync(Context.ConnectionId, ShipmentGroup(shipmentId));

    public Task UnwatchShipment(int shipmentId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, ShipmentGroup(shipmentId));

    public static string ShipmentGroup(int shipmentId) => $"shipment-{shipmentId}";
}

public interface ITowerClient
{
    Task Milestone(object message);
    Task AlertRaised(object message);
    Task AlertClosed(object message);
    Task EtaUpdated(object message);
    Task VehiclePosition(object message);
}

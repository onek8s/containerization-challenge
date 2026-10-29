using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Proseware.ControlTower.Portal.Services;

namespace Proseware.ControlTower.Portal.Hubs;

/// <summary>Live shipment updates for the portal. Each connection joins the group of its customer.</summary>
[Authorize(Policy = "Customer")]
public sealed class TrackingHub : Hub
{
    private readonly ILogger<TrackingHub> _logger;

    public TrackingHub(ILogger<TrackingHub> logger) => _logger = logger;

    public override async Task OnConnectedAsync()
    {
        if (CustomerContext.GetCustomerId(Context.User) is { } customerId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, CustomerContext.GroupName(customerId));
            _logger.LogDebug("Connection {Connection} joined customer {Customer}", Context.ConnectionId, customerId);
        }
        await base.OnConnectedAsync();
    }
}

public sealed record MilestoneUpdate(int ShipmentId, string Number, string Code, string Description, string? Location, string Status, DateTimeOffset OccurredAt);

public sealed record EtaUpdate(int ShipmentId, string Number, DateTimeOffset Eta, double RemainingKm);

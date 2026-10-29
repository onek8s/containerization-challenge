using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Integration.Messaging;

namespace Proseware.ControlTower.Scanning.Labels;

/// <summary>Prints labels requested from the booking application.</summary>
public sealed class LabelPrintListener : BackgroundService
{
    private readonly IEventBus _bus;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<LabelPrintListener> _logger;
    private IDisposable? _subscription;

    public LabelPrintListener(IEventBus bus, IServiceScopeFactory scopes, ILogger<LabelPrintListener> logger)
    {
        _bus = bus;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _subscription = await _bus.SubscribeAsync<LabelPrintRequested>("scan-labels", HandleAsync, stoppingToken);
    }

    private async Task HandleAsync(LabelPrintRequested message, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<ShipmentQueries>();
        var labels = scope.ServiceProvider.GetRequiredService<LabelService>();
        var shipment = await queries.GetAsync(message.ShipmentId, ct);
        if (shipment is null)
        {
            _logger.LogWarning("Label request for unknown shipment {ShipmentId}", message.ShipmentId);
            return;
        }
        await labels.PrintAsync(shipment, message.PackageId, message.PrinterName, message.RequestedBy, ct);
    }

    public override void Dispose()
    {
        _subscription?.Dispose();
        base.Dispose();
    }
}

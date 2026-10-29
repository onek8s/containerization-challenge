using Proseware.ControlTower.Documents;
using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Integration.Messaging;

namespace Proseware.ControlTower.Jobs.Jobs;

/// <summary>Generates documents requested from the booking application (the document service announces them).</summary>
public sealed class DocumentRequestSubscriber : BackgroundService
{
    private readonly IEventBus _bus;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<DocumentRequestSubscriber> _logger;
    private IDisposable? _subscription;

    public DocumentRequestSubscriber(IEventBus bus, IServiceScopeFactory scopes, ILogger<DocumentRequestSubscriber> logger)
    {
        _bus = bus;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && _subscription is null)
        {
            try
            {
                _subscription = await _bus.SubscribeAsync<DocumentRequested>("jobs-documents", HandleAsync, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Subscribing to document requests failed, retrying in 30 s");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    private async Task HandleAsync(DocumentRequested request, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IShipmentDocumentService>();
        var document = await service.CreateAsync(request.ShipmentId, request.Kind, request.RequestedBy, ct);
        _logger.LogInformation("{Kind} {DocumentId} created for shipment {ShipmentId} on request of {User}",
            request.Kind, document.Id, request.ShipmentId, request.RequestedBy);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        return base.StopAsync(cancellationToken);
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain.Documents;
using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Integration.Messaging;

namespace Proseware.ControlTower.Documents;

public sealed class ShipmentDocumentService : IShipmentDocumentService
{
    private readonly ShipmentQueries _queries;
    private readonly IShipmentDocumentRenderer _renderer;
    private readonly IDocumentStore _store;
    private readonly IEventBus _bus;
    private readonly DocumentOptions _options;
    private readonly ILogger<ShipmentDocumentService> _logger;

    public ShipmentDocumentService(ShipmentQueries queries, IShipmentDocumentRenderer renderer, IDocumentStore store, IEventBus bus,
        IOptions<DocumentOptions> options, ILogger<ShipmentDocumentService> logger)
    {
        _queries = queries;
        _renderer = renderer;
        _store = store;
        _bus = bus;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<StoredDocument> CreateAsync(int shipmentId, DocumentKind kind, string requestedBy, CancellationToken cancellationToken = default)
    {
        var shipment = await _queries.GetAsync(shipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment {shipmentId} not found.");
        if (!_renderer.Supports(kind))
            throw new NotSupportedException($"Document kind {kind} cannot be generated.");
        if (kind == DocumentKind.DangerousGoodsNote && !shipment.IsDangerousGoods)
            throw new InvalidOperationException($"Shipment {shipment.Number} has no dangerous goods.");

        var pdf = await _renderer.RenderPdfAsync(shipment, kind, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var metadata = new StoredDocument
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipment.Id,
            Kind = kind,
            FileName = $"{shipment.Number}-{kind}-{now:yyyyMMddHHmm}.pdf",
            ContentType = "application/pdf",
            VisibleToCustomer = kind is DocumentKind.Cmr or DocumentKind.HouseBillOfLading or DocumentKind.HouseAirWaybill or DocumentKind.DeliveryNote,
            CreatedBy = requestedBy,
            CreatedAt = now,
            DeleteAfter = _options.RetentionDays > 0 ? now.AddDays(_options.RetentionDays) : null
        };

        await using (var content = new MemoryStream(pdf, writable: false))
            metadata = await _store.SaveAsync(metadata, content, cancellationToken);

        await _bus.PublishAsync(new DocumentCreated(IntegrationEvent.NewId(), now, metadata.Id, shipment.Id, kind, metadata.FileName), cancellationToken);
        _logger.LogInformation("{Kind} {File} created for {Shipment} by {User}", kind, metadata.FileName, shipment.Number, requestedBy);
        return metadata;
    }
}

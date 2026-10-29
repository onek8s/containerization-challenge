using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Domain.Parties;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Integration.Carriers;
using Proseware.ControlTower.Integration.Edifact;
using Proseware.ControlTower.Integration.Messaging;
using Proseware.ControlTower.Integration.Sftp;

namespace Proseware.ControlTower.CarrierGateway;

/// <summary>Sends transport bookings to carriers: IFTMIN over SFTP or the carrier's REST API.</summary>
public sealed class CarrierBookingService : BackgroundService
{
    public const string SubscriptionId = "gateway-booking";

    private readonly IEventBus _bus;
    private readonly IServiceScopeFactory _scopes;
    private readonly IftminBuilder _iftmin;
    private readonly ControlReferences _references;
    private readonly IPartnerFileChannelFactory _channels;
    private readonly ICarrierApiClientResolver _apiClients;
    private readonly CarrierIntegrationOptions _carrierOptions;
    private readonly GatewayOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<CarrierBookingService> _logger;
    private IDisposable? _subscription;

    public CarrierBookingService(
        IEventBus bus,
        IServiceScopeFactory scopes,
        IftminBuilder iftmin,
        ControlReferences references,
        IPartnerFileChannelFactory channels,
        ICarrierApiClientResolver apiClients,
        IOptions<CarrierIntegrationOptions> carrierOptions,
        IOptions<GatewayOptions> options,
        IClock clock,
        ILogger<CarrierBookingService> logger)
    {
        _bus = bus;
        _scopes = scopes;
        _iftmin = iftmin;
        _references = references;
        _channels = channels;
        _apiClients = apiClients;
        _carrierOptions = carrierOptions.Value;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _subscription = await _bus.SubscribeAsync<CarrierBookingRequested>(SubscriptionId, HandleAsync, stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        return base.StopAsync(cancellationToken);
    }

    internal async Task HandleAsync(CarrierBookingRequested request, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ControlTowerDbContext>();
        var queries = scope.ServiceProvider.GetRequiredService<ShipmentQueries>();

        var shipment = await queries.Load(tracking: true).FirstOrDefaultAsync(s => s.Id == request.ShipmentId, ct);
        var leg = shipment?.Legs.FirstOrDefault(l => l.Id == request.LegId);
        var carrier = await db.Carriers.FindAsync(new object[] { request.CarrierId }, ct);
        if (shipment is null || leg is null || carrier is null)
        {
            _logger.LogWarning("Booking request for shipment {ShipmentId} leg {LegId} carrier {CarrierId} refers to missing data", request.ShipmentId, request.LegId, request.CarrierId);
            return;
        }

        switch (carrier.Integration)
        {
            case CarrierIntegration.EdifactSftp:
                await SendIftminAsync(db, shipment, leg, carrier, ct);
                break;
            case CarrierIntegration.RestApi:
                await BookOverApiAsync(db, shipment, leg, carrier, request.RequestedBy, ct);
                break;
            default:
                _logger.LogInformation("Carrier {Carrier} is booked manually; nothing sent for {Number} leg {Leg}", carrier.Code, shipment.Number, leg.Sequence);
                break;
        }
    }

    private async Task SendIftminAsync(ControlTowerDbContext db, Shipment shipment, Leg leg, Carrier carrier, CancellationToken ct)
    {
        if (!_options.EdiEnabled || !_carrierOptions.Sftp.TryGetValue(carrier.Code, out var sftp))
        {
            _logger.LogWarning("No SFTP settings for {Carrier}; IFTMIN for {Number} not sent", carrier.Code, shipment.Number);
            return;
        }

        var now = _clock.UtcNow;
        var controlRef = _references.Next(now);
        var function = string.IsNullOrEmpty(leg.CarrierReference) ? "9" : "5";
        var interchange = new EdifactInterchange
        {
            SenderId = _iftmin.OurPartyId,
            SenderQualifier = _iftmin.OurPartyQualifier,
            RecipientId = carrier.EdiPartnerId ?? carrier.Code,
            RecipientQualifier = carrier.EdiQualifier,
            Prepared = now.UtcDateTime,
            ControlReference = controlRef,
            ApplicationReference = IftminBuilder.MessageType
        };
        interchange.Messages.Add(_iftmin.Build(shipment, leg, carrier, "1", now, function));
        var text = EdifactWriter.Write(interchange);
        var bytes = EdifactInterchange.EncodingFor(interchange.SyntaxIdentifier).GetBytes(text);
        var fileName = ControlReferences.FileName(carrier.EdiPartnerId ?? carrier.Code, IftminBuilder.MessageType, controlRef);

        var log = new EdiInterchange
        {
            CarrierId = carrier.Id,
            Direction = InterchangeDirection.Outbound,
            MessageType = IftminBuilder.MessageType,
            ControlReference = controlRef,
            FileName = fileName,
            MessageCount = 1,
            CreatedAt = now,
            Payload = text
        };
        db.EdiInterchanges.Add(log);
        await File.WriteAllBytesAsync(Path.Combine(_options.Folder("outbound", carrier.Code), fileName), bytes, ct);

        try
        {
            using var channel = _channels.Open(sftp);
            channel.Upload(fileName, bytes);
            log.Status = InterchangeStatus.Sent;
            log.ProcessedAt = _clock.UtcNow;
            if (leg.Status == LegStatus.Planned) leg.Status = LegStatus.Confirmed;
            _logger.LogInformation("IFTMIN {ControlRef} for {Number} leg {Leg} sent to {Carrier}", controlRef, shipment.Number, leg.Sequence, carrier.Code);
        }
        catch (Exception ex)
        {
            log.Status = InterchangeStatus.Failed;
            log.Error = ex.Message;
            _logger.LogError(ex, "Sending IFTMIN {ControlRef} to {Carrier} failed", controlRef, carrier.Code);
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task BookOverApiAsync(ControlTowerDbContext db, Shipment shipment, Leg leg, Carrier carrier, string requestedBy, CancellationToken ct)
    {
        var client = _apiClients.Resolve(carrier);
        if (client is null)
        {
            _logger.LogWarning("API integration of {Carrier} is disabled; booking for {Number} not sent", carrier.Code, shipment.Number);
            return;
        }

        var now = _clock.UtcNow;
        var log = new EdiInterchange
        {
            CarrierId = carrier.Id,
            Direction = InterchangeDirection.Outbound,
            MessageType = "API",
            ControlReference = _references.Next(now),
            FileName = $"booking/{shipment.Number}-{leg.Sequence}",
            MessageCount = 1,
            CreatedAt = now
        };
        db.EdiInterchanges.Add(log);
        try
        {
            var result = await client.BookAsync(carrier, shipment, leg, ct);
            log.Payload = result.RawResponse;
            log.ProcessedAt = _clock.UtcNow;
            if (result.Accepted)
            {
                log.Status = InterchangeStatus.Acknowledged;
                leg.CarrierReference = result.CarrierReference ?? leg.CarrierReference;
                leg.Status = LegStatus.Confirmed;
                if (result.ConfirmedPickup is { } pickup && leg.Sequence == 1)
                {
                    ShipmentStateMachine.Apply(shipment, new Milestone
                    {
                        Code = MilestoneCodes.PickupPlanned,
                        OccurredAt = now,
                        RecordedAt = now,
                        LocationId = leg.FromId,
                        Source = "api:" + carrier.Code,
                        Remarks = $"Pickup confirmed for {pickup:yyyy-MM-dd HH:mm} UTC"
                    });
                }
                if (!string.IsNullOrEmpty(result.LabelZpl))
                {
                    var labelFile = Path.Combine(_options.Folder("labels", carrier.Code), $"{shipment.Number}-{leg.Sequence}.zpl");
                    await File.WriteAllTextAsync(labelFile, result.LabelZpl, Encoding.ASCII, ct);
                }
                _logger.LogInformation("{Carrier} accepted booking for {Number} leg {Leg}: {Reference}", carrier.Code, shipment.Number, leg.Sequence, result.CarrierReference);
            }
            else
            {
                log.Status = InterchangeStatus.Failed;
                log.Error = result.Message;
                _logger.LogWarning("{Carrier} declined booking for {Number}: {Message}", carrier.Code, shipment.Number, result.Message);
            }
        }
        catch (Exception ex)
        {
            log.Status = InterchangeStatus.Failed;
            log.Error = ex.Message;
            _logger.LogError(ex, "Booking {Number} with {Carrier} failed", shipment.Number, carrier.Code);
        }
        await db.SaveChangesAsync(ct);
        if (log.Status == InterchangeStatus.Acknowledged)
        {
            await _bus.PublishAsync(new ShipmentChanged(IntegrationEvent.NewId(), _clock.UtcNow, shipment.Id, shipment.Number,
                $"Leg {leg.Sequence} confirmed by {carrier.Code} ({leg.CarrierReference})", requestedBy), ct);
        }
    }
}

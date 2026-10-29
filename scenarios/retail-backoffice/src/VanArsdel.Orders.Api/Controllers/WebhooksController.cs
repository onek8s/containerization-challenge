using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using VanArsdel.BuildingBlocks.Outbox;
using VanArsdel.Contracts.Orders;
using VanArsdel.Integration.Carrier;
using VanArsdel.Integration.Payments;
using VanArsdel.Orders.Api.Data;
using VanArsdel.Orders.Api.Domain;
using VanArsdel.Orders.Api.Integration;

namespace VanArsdel.Orders.Api.Controllers;

[ApiController]
[Route("webhooks")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class WebhooksController : ControllerBase
{
    private readonly OrdersDbContext _db;
    private readonly IOutbox _outbox;
    private readonly PspOptions _psp;
    private readonly CarrierOptions _carrier;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(OrdersDbContext db, IOutbox outbox, IOptions<PspOptions> psp, IOptions<CarrierOptions> carrier, ILogger<WebhooksController> logger)
    {
        _db = db;
        _outbox = outbox;
        _psp = psp.Value;
        _carrier = carrier.Value;
        _logger = logger;
    }

    [HttpPost("psp")]
    public async Task<IActionResult> Psp(CancellationToken ct)
    {
        var body = await ReadBodyAsync();
        var valid = WebhookSignature.Verify(_psp.WebhookSecret, Request.Headers["Psp-Signature"], body, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5));
        var hook = JsonConvert.DeserializeObject<PspWebhook>(body);
        if (hook == null || string.IsNullOrEmpty(hook.Id)) return BadRequest();
        if (!await RecordAsync("psp", hook.Id, hook.Type, body, valid, ct)) return Ok();
        if (!valid)
        {
            _logger.LogWarning("PSP webhook {Id} has an invalid signature", hook.Id);
            await _db.SaveChangesAsync(ct);
            return Unauthorized();
        }

        var order = await _db.Orders.SingleOrDefaultAsync(o => o.PaymentReference == hook.PaymentId || o.OrderNo == hook.MerchantReference, ct);
        if (order == null)
        {
            _logger.LogWarning("PSP webhook {Id} for unknown payment {PaymentId}", hook.Id, hook.PaymentId);
            await _db.SaveChangesAsync(ct);
            return Ok();
        }

        switch (hook.Type)
        {
            case PspWebhookTypes.Authorized:
                _outbox.Add(new PaymentAuthorized { OrderId = order.Id, PaymentReference = hook.PaymentId, Amount = Money.FromMinor(hook.Amount) });
                break;
            case PspWebhookTypes.Declined:
                _outbox.Add(new PaymentFailed { OrderId = order.Id, PaymentReference = hook.PaymentId, Reason = hook.Reason });
                break;
            case PspWebhookTypes.ChargebackOpened:
                order.Log("Chargeback", $"Chargeback opened: {hook.Reason}", "psp");
                break;
            default:
                order.Log("Psp", hook.Type, "psp");
                break;
        }
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    [HttpPost("carrier")]
    public async Task<IActionResult> Carrier(CancellationToken ct)
    {
        var body = await ReadBodyAsync();
        var valid = string.Equals(Request.Headers["X-Carrier-Token"], _carrier.WebhookSecret, StringComparison.Ordinal) && !string.IsNullOrEmpty(_carrier.WebhookSecret);
        var e = JsonConvert.DeserializeObject<CarrierTrackingEvent>(body);
        if (e == null || string.IsNullOrEmpty(e.ShipmentId)) return BadRequest();
        if (!await RecordAsync("carrier", e.ShipmentId + ":" + e.Event, e.Event, body, valid, ct)) return Ok();
        if (!valid)
        {
            await _db.SaveChangesAsync(ct);
            return Unauthorized();
        }

        var shipment = await _db.Shipments.SingleOrDefaultAsync(s => s.CarrierShipmentId == e.ShipmentId, ct);
        if (shipment != null && (e.Event == CarrierEvents.Delivered || e.Event == CarrierEvents.ReadyForPickup))
        {
            _outbox.Add(new ShipmentDelivered { OrderId = shipment.OrderId, ShipmentId = shipment.Id, DeliveredUtc = e.Timestamp == default ? DateTime.UtcNow : e.Timestamp });
        }
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    private async Task<bool> RecordAsync(string source, string externalId, string type, string body, bool valid, CancellationToken ct)
    {
        if (await _db.InboundWebhooks.AnyAsync(w => w.Source == source && w.ExternalId == externalId, ct)) return false;
        _db.InboundWebhooks.Add(new InboundWebhook { Source = source, ExternalId = externalId, Type = type, Body = body, SignatureValid = valid, ReceivedUtc = DateTime.UtcNow });
        return true;
    }

    private async Task<string> ReadBodyAsync()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }
}

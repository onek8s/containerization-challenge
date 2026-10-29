using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanArsdel.BuildingBlocks.Exceptions;
using VanArsdel.BuildingBlocks.Outbox;
using VanArsdel.BuildingBlocks.Security;
using VanArsdel.Contracts;
using VanArsdel.Contracts.Orders;
using VanArsdel.Orders.Api.Data;
using VanArsdel.Orders.Api.Domain;

namespace VanArsdel.Orders.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize(Policy = Policies.Read)]
public class OrdersController : ControllerBase
{
    private readonly OrdersDbContext _db;
    private readonly OrderService _orders;
    private readonly IOutbox _outbox;

    public OrdersController(OrdersDbContext db, OrderService orders, IOutbox outbox)
    {
        _db = db;
        _orders = orders;
        _outbox = outbox;
    }

    /// <summary>Order search for customer service and the back office.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string status, [FromQuery] string email, [FromQuery] string channel,
        [FromQuery] string store, [FromQuery] DateTime? from, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var q = _db.Orders.AsNoTracking();
        if (!string.IsNullOrEmpty(status)) q = q.Where(o => o.Status == status);
        if (!string.IsNullOrEmpty(email)) q = q.Where(o => o.CustomerEmail == email.Trim().ToLower());
        if (!string.IsNullOrEmpty(channel)) q = q.Where(o => o.Channel == channel);
        if (!string.IsNullOrEmpty(store)) q = q.Where(o => o.CollectStoreCode == store || o.Shipments.Any(s => s.FromLocation == store));
        if (from.HasValue) q = q.Where(o => o.PlacedUtc >= from.Value);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(o => o.PlacedUtc).Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize)
            .Select(o => new
            {
                o.Id, o.OrderNo, o.Channel, o.Market, o.Status, o.CustomerEmail, o.DeliveryMethod, o.CollectStoreCode,
                o.Total, o.Currency, o.PaymentStatus, o.PlacedUtc, Shipments = o.Shipments.Count,
            }).ToListAsync();
        return Ok(new { page, pageSize, total, items });
    }

    [HttpGet("{orderNo}")]
    public async Task<IActionResult> Get(string orderNo, CancellationToken ct)
    {
        var order = await _orders.LoadByNoAsync(orderNo, ct) ?? throw new NotFoundException("Order", orderNo);
        var state = await _db.OrderStates.AsNoTracking().SingleOrDefaultAsync(s => s.CorrelationId == order.Id, ct);
        return Ok(new
        {
            order.Id, order.OrderNo, order.Channel, order.Market, order.Currency, order.Status, order.CustomerEmail, order.CustomerFirstName,
            order.CustomerLastName, order.CustomerPhone, order.LoyaltyCardNo, order.DeliveryMethod, order.CollectStoreCode,
            ShipTo = new { order.ShipToName, order.ShipToStreet, order.ShipToPostalCode, order.ShipToCity, order.ShipToCountry },
            order.ShippingFee, order.Total, order.PromotionCode, order.PaymentReference, order.PaymentMethod, order.PaymentStatus,
            order.CapturedAmount, order.PlacedUtc, order.CancelReason,
            Lines = order.Lines.OrderBy(l => l.LineNo).Select(l => new { l.LineNo, l.Sku, l.Quantity, l.UnitPrice, l.Discount }),
            Shipments = order.Shipments.Select(s => new
            {
                s.Id, s.FromLocation, s.FromLocationType, s.Status, s.Carrier, s.TrackingNo, s.TrackingUrl, s.LabelUrl, s.Parcels,
                s.PackedUtc, s.DispatchedUtc, s.DeliveredUtc, Lines = s.Lines.Select(l => new { l.Sku, l.Quantity }),
            }),
            Timeline = order.Events.OrderBy(e => e.CreatedUtc).Select(e => new { e.Type, e.Text, e.By, e.CreatedUtc }),
            Saga = state == null ? null : new { state.CurrentState, state.ShipmentsTotal, state.ShipmentsDispatched, state.ShipmentsDelivered },
        });
    }

    /// <summary>Endless-aisle order from a store (paid at the till).</summary>
    [HttpPost("store")]
    [Authorize(Policy = Policies.StoreOrder)]
    public async Task<IActionResult> CreateStoreOrder([FromBody] StoreOrderRequest request, CancellationToken ct)
    {
        if (!User.HasAnyRole(Roles.Service, Roles.Admin) && !string.Equals(User.StoreCode(), request.StoreCode, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }
        var order = await _orders.CreateStoreOrderAsync(request, User.UserName(), ct);
        return CreatedAtAction(nameof(Get), new { orderNo = order.OrderNo }, new { order.Id, order.OrderNo, order.Total, order.Status });
    }

    [HttpPost("{orderNo}/cancel")]
    [Authorize(Policy = Policies.Manage)]
    public async Task<IActionResult> Cancel(string orderNo, [FromBody] CancelRequest request, CancellationToken ct)
    {
        var order = await _orders.LoadByNoAsync(orderNo, ct) ?? throw new NotFoundException("Order", orderNo);
        if (order.Status == OrderStatus.Cancelled) return NoContent();
        if (!order.CanCancel) throw new DomainException($"Order {order.OrderNo} has shipments on the way and cannot be cancelled.");
        // The fulfilment consumer cancels the order and releases or refunds the payment.
        var reason = request.Reason ?? "Cancelled by customer service";
        order.Log("CancelRequested", reason, User.UserName());
        _outbox.Add(new Saga.CancelOrder { OrderId = order.Id, Reason = reason, CancelPayment = true });
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Packing confirmation from the DC warehouse system (stores confirm through the store operations API).</summary>
    [HttpPost("{orderNo}/shipments/{shipmentId:guid}/packed")]
    [Authorize(Policy = Policies.Manage)]
    public async Task<IActionResult> Packed(string orderNo, Guid shipmentId, [FromBody] PackedRequest request, CancellationToken ct)
    {
        var order = await _orders.LoadByNoAsync(orderNo, ct) ?? throw new NotFoundException("Order", orderNo);
        if (order.Shipments.All(s => s.Id != shipmentId)) throw new NotFoundException("Shipment", shipmentId);
        _outbox.Add(new ShipmentPacked { OrderId = order.Id, ShipmentId = shipmentId, Parcels = request.Parcels, WeightKg = request.WeightKg, PackedBy = User.UserName() });
        await _db.SaveChangesAsync(ct);
        return Accepted();
    }

    /// <summary>Cancels orders still waiting for payment after the limit (scheduler, every 15 minutes).</summary>
    [HttpPost("expire-unpaid")]
    [Authorize(Policy = Policies.Manage)]
    public async Task<IActionResult> ExpireUnpaid([FromQuery] int olderThanMinutes = 120, CancellationToken ct = default)
    {
        var limit = DateTime.UtcNow.AddMinutes(-Math.Max(15, olderThanMinutes));
        var stale = await _db.Orders.Where(o => o.Status == OrderStatus.AwaitingPayment && o.PlacedUtc < limit).Select(o => o.Id).ToListAsync(ct);
        foreach (var id in stale)
        {
            _outbox.Add(new Saga.CancelOrder { OrderId = id, Reason = "Payment not received", CancelPayment = true });
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new { cancelled = stale.Count });
    }

    [HttpGet("stats/today")]
    public async Task<IActionResult> Today()
    {
        var since = DateTime.UtcNow.Date;
        var rows = await _db.Orders.AsNoTracking().Where(o => o.PlacedUtc >= since).Select(o => new { o.Status, o.Channel, o.Total }).ToListAsync();
        return Ok(new
        {
            orders = rows.Count,
            value = rows.Sum(r => r.Total),
            byStatus = rows.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count()),
            byChannel = rows.GroupBy(r => r.Channel).ToDictionary(g => g.Key, g => g.Count()),
        });
    }
}

public class CancelRequest
{
    public string Reason { get; set; }
}

public class PackedRequest
{
    public int Parcels { get; set; } = 1;
    public decimal WeightKg { get; set; }
}

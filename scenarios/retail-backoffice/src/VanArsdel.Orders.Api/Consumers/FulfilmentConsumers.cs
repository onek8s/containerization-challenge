using MassTransit;
using Microsoft.EntityFrameworkCore;
using VanArsdel.BuildingBlocks.Outbox;
using VanArsdel.Contracts.Orders;
using VanArsdel.Orders.Api.Data;
using VanArsdel.Orders.Api.Domain;
using VanArsdel.Orders.Api.Integration;
using VanArsdel.Orders.Api.Saga;

namespace VanArsdel.Orders.Api.Consumers;

/// <summary>Order-side handling of the fulfilment events. Messages go out through the outbox.</summary>
public class FulfilmentConsumers :
    IConsumer<AllocateOrderRequested>,
    IConsumer<OrderAllocated>,
    IConsumer<ShipmentPacked>,
    IConsumer<ShipmentDelivered>,
    IConsumer<OrderCollected>,
    IConsumer<CancelOrder>,
    IConsumer<CapturePayment>,
    IConsumer<CompleteOrder>
{
    private readonly OrdersDbContext _db;
    private readonly OrderService _orders;
    private readonly IOutbox _outbox;
    private readonly CarrierService _carrier;
    private readonly PaymentService _payments;
    private readonly ILogger<FulfilmentConsumers> _logger;

    public FulfilmentConsumers(OrdersDbContext db, OrderService orders, IOutbox outbox, CarrierService carrier, PaymentService payments,
        ILogger<FulfilmentConsumers> logger)
    {
        _db = db;
        _orders = orders;
        _outbox = outbox;
        _carrier = carrier;
        _payments = payments;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AllocateOrderRequested> context)
    {
        var order = await Load(context.Message.OrderId, context.CancellationToken);
        order.Status = OrderStatus.Allocating;
        order.PaymentStatus = "Authorized";
        order.Log("PaymentAuthorized", "Payment authorized by the PSP");
        _outbox.Add(new AllocateOrder
        {
            OrderId = order.Id,
            OrderNo = order.OrderNo,
            DeliveryMethod = order.DeliveryMethod,
            ShipToCountry = order.ShipToCountry,
            CollectStoreCode = order.CollectStoreCode,
            Lines = order.Lines.Select(l => new OrderLineInfo { Sku = l.Sku, Quantity = l.Quantity, UnitPrice = l.UnitPrice, Discount = l.Discount }).ToList(),
        });
        await _db.SaveChangesAsync(context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<OrderAllocated> context)
    {
        var order = await Load(context.Message.OrderId, context.CancellationToken);
        if (order.Shipments.Count > 0 || order.Status == OrderStatus.Cancelled) return;

        foreach (var s in context.Message.Shipments)
        {
            var shipment = new Shipment
            {
                Id = s.ShipmentId,
                OrderId = order.Id,
                FromLocation = s.FromLocation,
                FromLocationType = s.FromLocationType,
                CreatedUtc = DateTime.UtcNow,
                Lines = s.Lines.Select(l => new ShipmentLine { Sku = l.Sku, Quantity = l.Quantity }).ToList(),
            };
            order.Shipments.Add(shipment);
            _db.Shipments.Add(shipment);
            _outbox.Add(new ShipmentRequested
            {
                OrderId = order.Id,
                OrderNo = order.OrderNo,
                ShipmentId = shipment.Id,
                FromLocation = shipment.FromLocation,
                FromLocationType = shipment.FromLocationType,
                DeliveryMethod = order.DeliveryMethod,
                CustomerName = $"{order.CustomerFirstName} {order.CustomerLastName}".Trim(),
                ShipTo = new OrderAddress { Name = order.ShipToName, Street = order.ShipToStreet, PostalCode = order.ShipToPostalCode, City = order.ShipToCity, Country = order.ShipToCountry },
                Lines = s.Lines,
            });
        }
        order.Status = OrderStatus.Confirmed;
        order.Log("Allocated", "Ships from " + string.Join(", ", context.Message.Shipments.Select(s => s.FromLocation)));
        _outbox.Add(new OrderConfirmed
        {
            OrderId = order.Id,
            OrderNo = order.OrderNo,
            Market = order.Market,
            CustomerEmail = order.CustomerEmail,
            CustomerPhone = order.CustomerPhone,
            Total = order.Total,
            Currency = order.Currency,
            ShipmentCount = context.Message.Shipments.Count,
        });
        await _db.SaveChangesAsync(context.CancellationToken);
    }

    /// <summary>
    /// Packed at the store or the DC. Click and collect from the same store is ready at once;
    /// everything else is booked with the carrier.
    /// </summary>
    public async Task Consume(ConsumeContext<ShipmentPacked> context)
    {
        var order = await Load(context.Message.OrderId, context.CancellationToken);
        var shipment = order.Shipments.SingleOrDefault(s => s.Id == context.Message.ShipmentId);
        if (shipment == null || shipment.Status != ShipmentStatus.Requested) return;

        shipment.Status = ShipmentStatus.Packed;
        shipment.PackedUtc = DateTime.UtcNow;
        shipment.PackedBy = context.Message.PackedBy;
        shipment.Parcels = Math.Max(1, context.Message.Parcels);
        shipment.WeightKg = context.Message.WeightKg;

        if (order.DeliveryMethod == DeliveryMethods.ClickAndCollect && string.Equals(shipment.FromLocation, order.CollectStoreCode, StringComparison.OrdinalIgnoreCase))
        {
            shipment.Status = ShipmentStatus.ReadyForCollection;
            shipment.DispatchedUtc = DateTime.UtcNow;
            _outbox.Add(new ShipmentDispatched { OrderId = order.Id, ShipmentId = shipment.Id, Carrier = "Store", TrackingNo = order.OrderNo });
            _outbox.Add(new OrderReadyForCollection
            {
                OrderId = order.Id,
                OrderNo = order.OrderNo,
                StoreCode = shipment.FromLocation,
                CustomerEmail = order.CustomerEmail,
                CustomerPhone = order.CustomerPhone,
                CollectBy = DateTime.UtcNow.Date.AddDays(8),
            });
            order.Log("ReadyForCollection", $"Ready at {shipment.FromLocation}", context.Message.PackedBy);
        }
        else
        {
            var booking = await _carrier.BookAsync(order, shipment, shipment.Parcels, shipment.WeightKg);
            shipment.Status = ShipmentStatus.Dispatched;
            shipment.DispatchedUtc = DateTime.UtcNow;
            shipment.Carrier = _carrier.Name;
            shipment.CarrierShipmentId = booking.Id;
            shipment.TrackingNo = booking.TrackingNumber;
            shipment.TrackingUrl = booking.TrackingUrl;
            shipment.LabelUrl = booking.LabelUrl;
            _outbox.Add(new ShipmentDispatched { OrderId = order.Id, ShipmentId = shipment.Id, Carrier = _carrier.Name, TrackingNo = booking.TrackingNumber, TrackingUrl = booking.TrackingUrl });
            _outbox.Add(new OrderShippedNotice
            {
                OrderId = order.Id,
                OrderNo = order.OrderNo,
                CustomerEmail = order.CustomerEmail,
                CustomerPhone = order.CustomerPhone,
                Carrier = _carrier.Name,
                TrackingNo = booking.TrackingNumber,
                TrackingUrl = booking.TrackingUrl,
            });
            order.Log("Dispatched", $"{shipment.FromLocation}: {_carrier.Name} {booking.TrackingNumber}", context.Message.PackedBy);
        }
        order.UpdateStatusFromShipments();
        await _db.SaveChangesAsync(context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<ShipmentDelivered> context)
    {
        var order = await Load(context.Message.OrderId, context.CancellationToken);
        var shipment = order.Shipments.SingleOrDefault(s => s.Id == context.Message.ShipmentId);
        if (shipment == null || shipment.Status == ShipmentStatus.Delivered) return;
        shipment.Status = order.DeliveryMethod == DeliveryMethods.ClickAndCollect ? ShipmentStatus.ReadyForCollection : ShipmentStatus.Delivered;
        shipment.DeliveredUtc = context.Message.DeliveredUtc;
        if (shipment.Status == ShipmentStatus.ReadyForCollection)
        {
            _outbox.Add(new OrderReadyForCollection
            {
                OrderId = order.Id,
                OrderNo = order.OrderNo,
                StoreCode = order.CollectStoreCode,
                CustomerEmail = order.CustomerEmail,
                CustomerPhone = order.CustomerPhone,
                CollectBy = DateTime.UtcNow.Date.AddDays(8),
            });
        }
        order.Log("Delivered", $"Shipment from {shipment.FromLocation} delivered");
        order.UpdateStatusFromShipments();
        await _db.SaveChangesAsync(context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<OrderCollected> context)
    {
        var order = await Load(context.Message.OrderId, context.CancellationToken);
        foreach (var s in order.Shipments.Where(s => s.Status == ShipmentStatus.ReadyForCollection)) s.Status = ShipmentStatus.Collected;
        order.Log("Collected", $"Collected at {context.Message.StoreCode}", context.Message.HandedOverBy);
        await _db.SaveChangesAsync(context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<CancelOrder> context)
    {
        var order = await Load(context.Message.OrderId, context.CancellationToken);
        if (order.Status == OrderStatus.Cancelled) return;
        order.Cancel(context.Message.Reason, "system");
        if (context.Message.CancelPayment && !string.IsNullOrEmpty(order.PaymentReference) && order.PaymentMethod != "POS")
        {
            if (order.PaymentStatus == "Captured")
            {
                // Uncollected click and collect: the money was taken when the parcel left the DC.
                var refunded = await _payments.RefundAsync(order.PaymentReference, order.CapturedAmount, order.OrderNo, context.Message.Reason);
                order.PaymentStatus = "Refunded";
                order.Log("Refunded", $"{refunded:0.00} {order.Currency}");
            }
            else
            {
                await _payments.CancelAsync(order.PaymentReference, order.OrderNo);
                order.PaymentStatus = "Cancelled";
            }
        }
        _outbox.Add(new OrderCancelled { OrderId = order.Id, OrderNo = order.OrderNo, Reason = context.Message.Reason, CustomerEmail = order.CustomerEmail });
        await _db.SaveChangesAsync(context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<CapturePayment> context)
    {
        var order = await Load(context.Message.OrderId, context.CancellationToken);
        if (order.PaymentMethod == "POS" || order.PaymentStatus == "Captured") return;
        var captured = await _payments.CaptureAsync(order.PaymentReference!, order.Total, order.OrderNo);
        order.CapturedAmount = captured;
        order.PaymentStatus = "Captured";
        order.Log("PaymentCaptured", $"{captured:0.00} {order.Currency}");
        _outbox.Add(new PaymentCaptured { OrderId = order.Id, PaymentReference = order.PaymentReference, Amount = captured, Currency = order.Currency });
        await _db.SaveChangesAsync(context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<CompleteOrder> context)
    {
        var order = await Load(context.Message.OrderId, context.CancellationToken);
        if (order.Status == OrderStatus.Completed) return;
        order.Status = OrderStatus.Completed;
        order.Log("Completed", null);
        _outbox.Add(new OrderCompleted
        {
            OrderId = order.Id,
            OrderNo = order.OrderNo,
            Channel = order.Channel,
            Market = order.Market,
            Currency = order.Currency,
            CustomerEmail = order.CustomerEmail,
            LoyaltyCardNo = order.LoyaltyCardNo,
            Total = order.Total,
            CompletedUtc = DateTime.UtcNow,
            Lines = order.Lines.Select(l => new OrderLineInfo { Sku = l.Sku, Quantity = l.Quantity, UnitPrice = l.UnitPrice, Discount = l.Discount }).ToList(),
        });
        await _db.SaveChangesAsync(context.CancellationToken);
    }

    private async Task<Order> Load(Guid id, CancellationToken ct) =>
        await _orders.LoadAsync(id, ct) ?? throw new InvalidOperationException($"Order {id} not found");
}

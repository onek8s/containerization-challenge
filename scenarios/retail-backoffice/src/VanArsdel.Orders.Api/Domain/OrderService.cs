using MassTransit;
using Microsoft.EntityFrameworkCore;
using VanArsdel.BuildingBlocks.Exceptions;
using VanArsdel.BuildingBlocks.Outbox;
using VanArsdel.Contracts;
using VanArsdel.Contracts.Orders;
using VanArsdel.Orders.Api.Data;

namespace VanArsdel.Orders.Api.Domain;

public class OrderService
{
    private readonly OrdersDbContext _db;
    private readonly IOutbox _outbox;
    private readonly ILogger<OrderService> _logger;

    public OrderService(OrdersDbContext db, IOutbox outbox, ILogger<OrderService> logger)
    {
        _db = db;
        _outbox = outbox;
        _logger = logger;
    }

    /// <summary>Creates the order from the web shop hand-over. Returns null if it was received before.</summary>
    public async Task<Order> CreateFromWebShopAsync(WebShopOrderSubmitted m, CancellationToken ct)
    {
        var orderNo = "W" + m.WebOrderNo;
        if (await _db.Orders.AnyAsync(o => o.OrderNo == orderNo, ct))
        {
            _logger.LogInformation("Web order {OrderNo} already received", orderNo);
            return null;
        }

        var order = new Order
        {
            Id = NewId.NextGuid(),
            OrderNo = orderNo,
            Channel = "Web",
            Market = m.Market.ToUpperInvariant(),
            Currency = m.Currency ?? Markets.CurrencyOf(m.Market),
            CustomerEmail = m.Customer.Email.Trim().ToLowerInvariant(),
            CustomerFirstName = m.Customer.FirstName,
            CustomerLastName = m.Customer.LastName,
            CustomerPhone = m.Customer.Phone,
            LoyaltyCardNo = m.Customer.LoyaltyCardNo,
            DeliveryMethod = m.DeliveryMethod,
            CollectStoreCode = m.CollectStoreCode,
            ShipToName = m.ShippingAddress?.Name,
            ShipToStreet = m.ShippingAddress?.Street,
            ShipToPostalCode = m.ShippingAddress?.PostalCode,
            ShipToCity = m.ShippingAddress?.City,
            ShipToCountry = m.ShippingAddress?.Country ?? m.Market.ToUpperInvariant(),
            ShippingFee = m.ShippingFee,
            PromotionCode = m.PromotionCode,
            PaymentReference = m.PaymentReference,
            PaymentMethod = m.PaymentMethod,
            PaymentStatus = "Pending",
            CreatedBy = "webshop",
            PlacedUtc = m.PlacedUtc == default ? DateTime.UtcNow : m.PlacedUtc,
            UpdatedUtc = DateTime.UtcNow,
        };
        var lineNo = 1;
        foreach (var l in m.Lines)
        {
            order.Lines.Add(new OrderLine { LineNo = lineNo++, Sku = l.Sku.ToUpperInvariant(), Quantity = l.Quantity, UnitPrice = l.UnitPrice, Discount = l.Discount });
        }
        order.Recalculate();
        order.Log("Received", $"Web order {m.WebOrderNo}, {order.Lines.Count} lines, {order.Total:0.00} {order.Currency}");

        Submit(order, paymentAuthorized: false);
        await _db.SaveChangesAsync(ct);
        return order;
    }

    /// <summary>Endless aisle: the customer paid at the till; the order ships from the DC or another store.</summary>
    public async Task<Order> CreateStoreOrderAsync(StoreOrderRequest r, string user, CancellationToken ct)
    {
        if (r.Lines.Count == 0) throw new DomainException("The order has no lines.");
        var order = new Order
        {
            Id = NewId.NextGuid(),
            OrderNo = $"S{r.StoreCode}-{DateTime.UtcNow:yyMMddHHmmss}",
            Channel = "Store",
            Market = r.Market.ToUpperInvariant(),
            Currency = Markets.CurrencyOf(r.Market),
            CustomerEmail = r.CustomerEmail.Trim().ToLowerInvariant(),
            CustomerFirstName = r.FirstName,
            CustomerLastName = r.LastName,
            CustomerPhone = r.Phone,
            LoyaltyCardNo = r.LoyaltyCardNo,
            DeliveryMethod = r.DeliveryMethod,
            CollectStoreCode = r.DeliveryMethod == DeliveryMethods.ClickAndCollect ? r.StoreCode : null,
            ShipToName = r.ShipTo?.Name,
            ShipToStreet = r.ShipTo?.Street,
            ShipToPostalCode = r.ShipTo?.PostalCode,
            ShipToCity = r.ShipTo?.City,
            ShipToCountry = r.ShipTo?.Country ?? r.Market.ToUpperInvariant(),
            PaymentReference = r.PosReceiptNo,
            PaymentMethod = "POS",
            PaymentStatus = "Paid",
            CreatedBy = user,
            PlacedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
        };
        var lineNo = 1;
        foreach (var l in r.Lines)
        {
            order.Lines.Add(new OrderLine { LineNo = lineNo++, Sku = l.Sku.ToUpperInvariant(), Quantity = l.Quantity, UnitPrice = l.UnitPrice, Discount = l.Discount });
        }
        order.Recalculate();
        order.CapturedAmount = order.Total;
        order.Log("Received", $"Store order at {r.StoreCode}, paid on receipt {r.PosReceiptNo}", user);

        _db.Orders.Add(order);
        Submit(order, paymentAuthorized: true, add: false);
        await _db.SaveChangesAsync(ct);
        return order;
    }

    private void Submit(Order order, bool paymentAuthorized, bool add = true)
    {
        if (add) _db.Orders.Add(order);
        order.Status = paymentAuthorized ? OrderStatus.Allocating : OrderStatus.AwaitingPayment;
        _outbox.Add(new OrderSubmitted
        {
            OrderId = order.Id,
            OrderNo = order.OrderNo,
            Channel = order.Channel,
            Market = order.Market,
            Currency = order.Currency,
            CustomerEmail = order.CustomerEmail,
            CustomerPhone = order.CustomerPhone,
            LoyaltyCardNo = order.LoyaltyCardNo,
            DeliveryMethod = order.DeliveryMethod,
            CollectStoreCode = order.CollectStoreCode,
            ShipToCountry = order.ShipToCountry,
            Total = order.Total,
            PaymentReference = order.PaymentReference,
            PaymentAuthorized = paymentAuthorized,
            Lines = order.Lines.Select(l => new OrderLineInfo { Sku = l.Sku, Quantity = l.Quantity, UnitPrice = l.UnitPrice, Discount = l.Discount }).ToList(),
        });
    }

    public Task<Order> LoadAsync(Guid id, CancellationToken ct) =>
        _db.Orders.Include(o => o.Lines).Include(o => o.Shipments).ThenInclude(s => s.Lines)
            .SingleOrDefaultAsync(o => o.Id == id, ct);

    public Task<Order> LoadByNoAsync(string orderNo, CancellationToken ct) =>
        _db.Orders.Include(o => o.Lines).Include(o => o.Shipments).ThenInclude(s => s.Lines).Include(o => o.Events)
            .SingleOrDefaultAsync(o => o.OrderNo == orderNo, ct);
}

public class StoreOrderRequest
{
    public string StoreCode { get; set; } = "";
    public string Market { get; set; } = "FI";
    public string PosReceiptNo { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Phone { get; set; }
    public string LoyaltyCardNo { get; set; }
    public string DeliveryMethod { get; set; } = DeliveryMethods.Home;
    public OrderAddress ShipTo { get; set; }
    public List<OrderLineInfo> Lines { get; set; } = new();
}

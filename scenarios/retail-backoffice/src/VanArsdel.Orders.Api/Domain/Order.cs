using VanArsdel.BuildingBlocks.Exceptions;
using VanArsdel.Contracts.Orders;

namespace VanArsdel.Orders.Api.Domain;

public static class OrderStatus
{
    public const string AwaitingPayment = "AwaitingPayment";
    public const string Allocating = "Allocating";
    public const string Confirmed = "Confirmed";
    public const string PartlyShipped = "PartlyShipped";
    public const string Shipped = "Shipped";
    public const string ReadyForCollection = "ReadyForCollection";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

public static class ShipmentStatus
{
    public const string Requested = "Requested";
    public const string Packed = "Packed";
    public const string Dispatched = "Dispatched";
    public const string Delivered = "Delivered";
    public const string ReadyForCollection = "ReadyForCollection";
    public const string Collected = "Collected";
    public const string Cancelled = "Cancelled";
}

public class Order
{
    public Guid Id { get; set; }
    public string OrderNo { get; set; } = "";
    public string Channel { get; set; } = "Web";
    public string Market { get; set; } = "FI";
    public string Currency { get; set; } = "EUR";
    public string Status { get; set; } = OrderStatus.AwaitingPayment;
    public string CustomerEmail { get; set; } = "";
    public string CustomerFirstName { get; set; }
    public string CustomerLastName { get; set; }
    public string CustomerPhone { get; set; }
    public string LoyaltyCardNo { get; set; }
    public string DeliveryMethod { get; set; } = DeliveryMethods.Home;
    public string CollectStoreCode { get; set; }
    public string ShipToName { get; set; }
    public string ShipToStreet { get; set; }
    public string ShipToPostalCode { get; set; }
    public string ShipToCity { get; set; }
    public string ShipToCountry { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Total { get; set; }
    public string PromotionCode { get; set; }
    public string PaymentReference { get; set; }
    public string PaymentMethod { get; set; }
    public string PaymentStatus { get; set; } = "Pending";
    public decimal CapturedAmount { get; set; }
    public string CreatedBy { get; set; }
    public DateTime PlacedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public string CancelReason { get; set; }
    public byte[] RowVersion { get; set; }
    public List<OrderLine> Lines { get; set; } = new();
    public List<Shipment> Shipments { get; set; } = new();
    public List<OrderEvent> Events { get; set; } = new();

    public decimal LinesTotal => Lines.Sum(l => l.Quantity * l.UnitPrice - l.Discount);

    public void Recalculate() => Total = LinesTotal + ShippingFee;

    public void Log(string type, string text, string by = "system")
    {
        Events.Add(new OrderEvent { Type = type, Text = text, By = by, CreatedUtc = DateTime.UtcNow });
        UpdatedUtc = DateTime.UtcNow;
    }

    public bool CanCancel => (Status is OrderStatus.AwaitingPayment or OrderStatus.Allocating or OrderStatus.Confirmed
                              && Shipments.All(s => s.Status is ShipmentStatus.Requested or ShipmentStatus.Cancelled))
                             || IsUncollected;

    /// <summary>Click and collect parcel still waiting in the store: it goes back to store stock when not collected.</summary>
    public bool IsUncollected => Status == OrderStatus.ReadyForCollection
                                 && Shipments.All(s => s.Status is ShipmentStatus.ReadyForCollection or ShipmentStatus.Cancelled);

    public void Cancel(string reason, string by)
    {
        if (Status == OrderStatus.Cancelled) return;
        if (!CanCancel) throw new DomainException($"Order {OrderNo} has shipments on the way and cannot be cancelled.");
        Status = OrderStatus.Cancelled;
        CancelReason = reason;
        foreach (var s in Shipments) s.Status = ShipmentStatus.Cancelled;
        Log("Cancelled", reason, by);
    }

    public void UpdateStatusFromShipments()
    {
        var active = Shipments.Where(s => s.Status != ShipmentStatus.Cancelled).ToList();
        if (active.Count == 0 || Status == OrderStatus.Cancelled || Status == OrderStatus.Completed) return;
        if (active.All(s => s.Status is ShipmentStatus.Delivered or ShipmentStatus.Collected)) return;
        if (active.Any(s => s.Status == ShipmentStatus.ReadyForCollection) && DeliveryMethod == DeliveryMethods.ClickAndCollect)
        {
            Status = OrderStatus.ReadyForCollection;
        }
        else if (active.All(s => s.Status is ShipmentStatus.Dispatched or ShipmentStatus.Delivered))
        {
            Status = OrderStatus.Shipped;
        }
        else if (active.Any(s => s.Status is ShipmentStatus.Dispatched or ShipmentStatus.Delivered))
        {
            Status = OrderStatus.PartlyShipped;
        }
    }
}

public class OrderLine
{
    public long Id { get; set; }
    public Guid OrderId { get; set; }
    public int LineNo { get; set; }
    public string Sku { get; set; } = "";
    public string Description { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
}

public class Shipment
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string FromLocation { get; set; } = "";
    public string FromLocationType { get; set; } = "";
    public string Status { get; set; } = ShipmentStatus.Requested;
    public string Carrier { get; set; }
    public string CarrierShipmentId { get; set; }
    public string TrackingNo { get; set; }
    public string TrackingUrl { get; set; }
    public string LabelUrl { get; set; }
    public int Parcels { get; set; }
    public decimal WeightKg { get; set; }
    public string PackedBy { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? PackedUtc { get; set; }
    public DateTime? DispatchedUtc { get; set; }
    public DateTime? DeliveredUtc { get; set; }
    public List<ShipmentLine> Lines { get; set; } = new();
}

public class ShipmentLine
{
    public long Id { get; set; }
    public Guid ShipmentId { get; set; }
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
}

public class OrderEvent
{
    public long Id { get; set; }
    public Guid OrderId { get; set; }
    public string Type { get; set; } = "";
    public string Text { get; set; }
    public string By { get; set; }
    public DateTime CreatedUtc { get; set; }
}

/// <summary>Inbound webhook calls, kept for idempotency and support.</summary>
public class InboundWebhook
{
    public long Id { get; set; }
    public string Source { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public string Type { get; set; }
    public string Body { get; set; }
    public bool SignatureValid { get; set; }
    public DateTime ReceivedUtc { get; set; }
}

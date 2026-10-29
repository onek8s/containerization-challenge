using System;
using System.Collections.Generic;

namespace VanArsdel.Contracts.Orders
{
    /// <summary>Order handed over by the web shop platform (Service Bus queue "webshop-orders").</summary>
    public class WebShopOrderSubmitted
    {
        public string WebOrderNo { get; set; }
        public string Market { get; set; }
        public string Currency { get; set; }
        public DateTime PlacedUtc { get; set; }
        public OrderCustomer Customer { get; set; }
        public string DeliveryMethod { get; set; }
        public string CollectStoreCode { get; set; }
        public OrderAddress ShippingAddress { get; set; }
        public List<OrderLineInfo> Lines { get; set; } = new List<OrderLineInfo>();
        public decimal ShippingFee { get; set; }
        public string PaymentReference { get; set; }
        public string PaymentMethod { get; set; }
        public string PromotionCode { get; set; }
    }

    public class OrderCustomer
    {
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public string LoyaltyCardNo { get; set; }
        public bool MarketingConsent { get; set; }
    }

    public class OrderAddress
    {
        public string Name { get; set; }
        public string Street { get; set; }
        public string PostalCode { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
    }

    public class OrderLineInfo
    {
        public string Sku { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
    }

    public static class DeliveryMethods
    {
        public const string Home = "Home";
        public const string ParcelLocker = "ParcelLocker";
        public const string ClickAndCollect = "ClickAndCollect";
    }

    public class OrderSubmitted
    {
        public Guid OrderId { get; set; }
        public string OrderNo { get; set; }
        public string Channel { get; set; }
        public string Market { get; set; }
        public string Currency { get; set; }
        public string CustomerEmail { get; set; }
        public string CustomerPhone { get; set; }
        public string LoyaltyCardNo { get; set; }
        public string DeliveryMethod { get; set; }
        public string CollectStoreCode { get; set; }
        public string ShipToCountry { get; set; }
        public decimal Total { get; set; }
        public string PaymentReference { get; set; }
        public bool PaymentAuthorized { get; set; }
        public List<OrderLineInfo> Lines { get; set; } = new List<OrderLineInfo>();
    }

    public class PaymentAuthorized
    {
        public Guid OrderId { get; set; }
        public string PaymentReference { get; set; }
        public decimal Amount { get; set; }
    }

    public class PaymentFailed
    {
        public Guid OrderId { get; set; }
        public string PaymentReference { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>Command to the allocation service.</summary>
    public class AllocateOrder
    {
        public Guid OrderId { get; set; }
        public string OrderNo { get; set; }
        public string DeliveryMethod { get; set; }
        public string ShipToCountry { get; set; }
        public string CollectStoreCode { get; set; }
        public List<OrderLineInfo> Lines { get; set; } = new List<OrderLineInfo>();
    }

    public class OrderAllocated
    {
        public Guid OrderId { get; set; }
        public Guid AllocationId { get; set; }
        public List<AllocatedShipment> Shipments { get; set; } = new List<AllocatedShipment>();
    }

    public class AllocatedShipment
    {
        public Guid ShipmentId { get; set; }
        public string FromLocation { get; set; }
        public string FromLocationType { get; set; }
        public List<StockLineRef> Lines { get; set; } = new List<StockLineRef>();
    }

    public class StockLineRef
    {
        public string Sku { get; set; }
        public int Quantity { get; set; }
    }

    public class OrderAllocationFailed
    {
        public Guid OrderId { get; set; }
        public string Reason { get; set; }
        public List<StockLineRef> ShortLines { get; set; } = new List<StockLineRef>();
    }

    /// <summary>A shipment is to be picked at a store (ship-from-store or click and collect) or at the DC.</summary>
    public class ShipmentRequested
    {
        public Guid OrderId { get; set; }
        public string OrderNo { get; set; }
        public Guid ShipmentId { get; set; }
        public string FromLocation { get; set; }
        public string FromLocationType { get; set; }
        public string DeliveryMethod { get; set; }
        public string CustomerName { get; set; }
        public OrderAddress ShipTo { get; set; }
        public List<StockLineRef> Lines { get; set; } = new List<StockLineRef>();
    }

    public class ShipmentPacked
    {
        public Guid OrderId { get; set; }
        public Guid ShipmentId { get; set; }
        public int Parcels { get; set; }
        public decimal WeightKg { get; set; }
        public string PackedBy { get; set; }
    }

    public class ShipmentDispatched
    {
        public Guid OrderId { get; set; }
        public Guid ShipmentId { get; set; }
        public string Carrier { get; set; }
        public string TrackingNo { get; set; }
        public string TrackingUrl { get; set; }
    }

    public class ShipmentDelivered
    {
        public Guid OrderId { get; set; }
        public Guid ShipmentId { get; set; }
        public DateTime DeliveredUtc { get; set; }
    }

    public class OrderReadyForCollection
    {
        public Guid OrderId { get; set; }
        public string OrderNo { get; set; }
        public string StoreCode { get; set; }
        public string CustomerEmail { get; set; }
        public string CustomerPhone { get; set; }
        public DateTime CollectBy { get; set; }
    }

    public class OrderCollected
    {
        public Guid OrderId { get; set; }
        public string StoreCode { get; set; }
        public string HandedOverBy { get; set; }
    }

    /// <summary>Click and collect parcel was not picked up in time and goes back to stock.</summary>
    public class CollectionExpired
    {
        public Guid OrderId { get; set; }
        public string StoreCode { get; set; }
        public DateTime CollectBy { get; set; }
    }

    public class OrderConfirmed
    {
        public Guid OrderId { get; set; }
        public string OrderNo { get; set; }
        public string Market { get; set; }
        public string CustomerEmail { get; set; }
        public string CustomerPhone { get; set; }
        public decimal Total { get; set; }
        public string Currency { get; set; }
        public int ShipmentCount { get; set; }
    }

    public class OrderShippedNotice
    {
        public Guid OrderId { get; set; }
        public string OrderNo { get; set; }
        public string CustomerEmail { get; set; }
        public string CustomerPhone { get; set; }
        public string Carrier { get; set; }
        public string TrackingNo { get; set; }
        public string TrackingUrl { get; set; }
    }

    public class OrderCompleted
    {
        public Guid OrderId { get; set; }
        public string OrderNo { get; set; }
        public string Channel { get; set; }
        public string Market { get; set; }
        public string Currency { get; set; }
        public string CustomerEmail { get; set; }
        public string LoyaltyCardNo { get; set; }
        public decimal Total { get; set; }
        public DateTime CompletedUtc { get; set; }
        public List<OrderLineInfo> Lines { get; set; } = new List<OrderLineInfo>();
    }

    public class OrderCancelled
    {
        public Guid OrderId { get; set; }
        public string OrderNo { get; set; }
        public string Reason { get; set; }
        public string CustomerEmail { get; set; }
    }

    public class PaymentCaptured
    {
        public Guid OrderId { get; set; }
        public string PaymentReference { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
    }
}

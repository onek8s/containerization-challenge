using System;
using System.Collections.Generic;

namespace VanArsdel.StoreOps.Api.Domain
{
    public class Store
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Region { get; set; }
        public string Country { get; set; }
        public string City { get; set; }
        public string Street { get; set; }
        public string PostalCode { get; set; }
        public string Currency { get; set; }
        public string TimeZone { get; set; } = "Europe/Helsinki";
        public string Phone { get; set; }
        public string ManagerEmail { get; set; }
        public bool ShipFromStore { get; set; }
        public bool ClickAndCollect { get; set; }
        public string OpeningHours { get; set; }
        public DateTime OpenedOn { get; set; }
        public DateTime? ClosedOn { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    public static class PickTaskStatus
    {
        public const string Open = "Open";
        public const string Picking = "Picking";
        public const string Packed = "Packed";
        public const string Cancelled = "Cancelled";
    }

    /// <summary>Ship-from-store or click and collect order to pick at a store.</summary>
    public class PickTask
    {
        public Guid Id { get; set; }
        public string StoreCode { get; set; }
        public Guid OrderId { get; set; }
        public string OrderNo { get; set; }
        public Guid ShipmentId { get; set; }
        public string DeliveryMethod { get; set; }
        public string CustomerName { get; set; }
        public string Status { get; set; } = PickTaskStatus.Open;
        public string AssignedTo { get; set; }
        public int Parcels { get; set; }
        public decimal WeightKg { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime DueUtc { get; set; }
        public DateTime? PackedUtc { get; set; }
        public List<PickTaskLine> Lines { get; set; } = new List<PickTaskLine>();
    }

    public class PickTaskLine
    {
        public long Id { get; set; }
        public Guid PickTaskId { get; set; }
        public string Sku { get; set; }
        public int Quantity { get; set; }
        public int Picked { get; set; }
    }

    public static class CollectionStatus
    {
        public const string Waiting = "Waiting";
        public const string Collected = "Collected";
        public const string Expired = "Expired";
    }

    /// <summary>Click and collect parcel on the collection shelf.</summary>
    public class Collection
    {
        public long Id { get; set; }
        public string StoreCode { get; set; }
        public Guid OrderId { get; set; }
        public string OrderNo { get; set; }
        public string CustomerEmail { get; set; }
        public string CustomerPhone { get; set; }
        public string Status { get; set; } = CollectionStatus.Waiting;
        public DateTime ReadyUtc { get; set; }
        public DateTime CollectBy { get; set; }
        public DateTime? CollectedUtc { get; set; }
        public string HandedOverBy { get; set; }
    }

    /// <summary>Daily totals from the POS feed.</summary>
    public class DailyTakings
    {
        public long Id { get; set; }
        public string StoreCode { get; set; }
        public DateTime BusinessDate { get; set; }
        public int Receipts { get; set; }
        public int Items { get; set; }
        public decimal NetSales { get; set; }
        public decimal ReturnsValue { get; set; }
        public decimal CashTotal { get; set; }
        public decimal CardTotal { get; set; }
        public decimal OtherTotal { get; set; }
        public int LoyaltyReceipts { get; set; }
        public string LastFile { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    public class CashUp
    {
        public long Id { get; set; }
        public string StoreCode { get; set; }
        public DateTime BusinessDate { get; set; }
        public decimal ExpectedCash { get; set; }
        public decimal CountedCash { get; set; }
        public decimal Difference { get; set; }
        public string CountedBy { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    /// <summary>Visitor counts per hour from the door counters.</summary>
    public class Footfall
    {
        public long Id { get; set; }
        public string StoreCode { get; set; }
        public DateTime Date { get; set; }
        public int Hour { get; set; }
        public int Visitors { get; set; }
    }
}

using System;
using System.Collections.Generic;

namespace VanArsdel.Inventory.Api.Domain
{
    public class Location
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Country { get; set; }
        public string City { get; set; }
        public bool ShipFromStore { get; set; }
        public bool ClickAndCollect { get; set; }
        public bool Active { get; set; } = true;

        /// <summary>Units per SKU kept back from web orders (shop floor display, damaged).</summary>
        public int SafetyStock { get; set; } = 1;

        public DateTime UpdatedUtc { get; set; }
    }

    public class Item
    {
        public string Sku { get; set; }
        public string StyleCode { get; set; }
        public string Ean { get; set; }
        public string CategoryCode { get; set; }
        public bool Discontinued { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public class StockLevel
    {
        public long Id { get; set; }
        public string LocationCode { get; set; }
        public string Sku { get; set; }
        public int OnHand { get; set; }
        public int Reserved { get; set; }
        public int InTransit { get; set; }
        public int MinLevel { get; set; }
        public int MaxLevel { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public byte[] RowVersion { get; set; }

        public int Available => Math.Max(0, OnHand - Reserved);
    }

    public static class MovementTypes
    {
        public const string Sale = "Sale";
        public const string PosReturn = "PosReturn";
        public const string Receipt = "Receipt";
        public const string TransferIn = "TransferIn";
        public const string TransferOut = "TransferOut";
        public const string Adjustment = "Adjustment";
        public const string StockCount = "StockCount";
        public const string Reserve = "Reserve";
        public const string Release = "Release";
        public const string Pick = "Pick";
        public const string ReturnRestock = "ReturnRestock";
    }

    public class StockMovement
    {
        public long Id { get; set; }
        public string LocationCode { get; set; }
        public string Sku { get; set; }
        public int Quantity { get; set; }
        public string Type { get; set; }
        public string Reference { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public static class ReservationStatus
    {
        public const string Active = "Active";
        public const string Committed = "Committed";
        public const string Released = "Released";
    }

    public class Reservation
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Guid ShipmentId { get; set; }
        public string LocationCode { get; set; }
        public string Sku { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime ExpiresUtc { get; set; }
        public DateTime? ClosedUtc { get; set; }
    }

    public class ProcessedMessage
    {
        public string Key { get; set; }
        public DateTime ProcessedUtc { get; set; }
    }

    public class ReplenishmentProposal
    {
        public Guid Id { get; set; }
        public string StoreCode { get; set; }
        public string FromLocation { get; set; }
        public string Status { get; set; }
        public DateTime CreatedUtc { get; set; }
        public List<ReplenishmentLine> Lines { get; set; } = new List<ReplenishmentLine>();
    }

    public class ReplenishmentLine
    {
        public long Id { get; set; }
        public Guid ProposalId { get; set; }
        public string Sku { get; set; }
        public int Quantity { get; set; }
        public int OnHand { get; set; }
        public int MaxLevel { get; set; }
    }
}

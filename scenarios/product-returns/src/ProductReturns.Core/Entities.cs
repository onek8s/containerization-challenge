using System;
using System.Collections.Generic;

namespace ProductReturns.Core
{
    /// <summary>Sales order header replicated from the ERP every night (read only).</summary>
    public class SalesOrder
    {
        public string OrderNumber { get; set; }
        public string CustomerNumber { get; set; }
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public string ShipToName { get; set; }
        public string ShipToStreet { get; set; }
        public string ShipToPostalCode { get; set; }
        public string ShipToCity { get; set; }
        public string ShipToCountry { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime? DeliveredDate { get; set; }
        public string Currency { get; set; }
        public List<SalesOrderLine> Lines { get; set; } = new List<SalesOrderLine>();
    }

    public class SalesOrderLine
    {
        public string OrderNumber { get; set; }
        public int LineNumber { get; set; }
        public string Sku { get; set; }
        public string Description { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    /// <summary>Product master data from the ERP item view.</summary>
    public class CatalogItem
    {
        public string Sku { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public bool FinalSale { get; set; }
        public bool Returnable { get; set; } = true;
        public decimal WeightKg { get; set; }
    }

    public class ReturnRequest
    {
        public int Id { get; set; }
        public string RmaNumber { get; set; }
        public string OrderNumber { get; set; }
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public string Currency { get; set; } = "EUR";
        public ReturnStatus Status { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? DecidedUtc { get; set; }
        public string DecidedBy { get; set; }
        public string RejectReason { get; set; }

        public string TrackingNumber { get; set; }
        public string LabelPath { get; set; }
        public DateTime? LabelCreatedUtc { get; set; }
        public int LabelAttempts { get; set; }
        public string LabelError { get; set; }
        public DateTime? ReminderSentUtc { get; set; }

        public DateTime? ReceivedUtc { get; set; }
        public DateTime? InspectedUtc { get; set; }
        public string InspectedBy { get; set; }
        public decimal? RefundAmount { get; set; }
        public decimal? RestockingFee { get; set; }
        public decimal? LabelFee { get; set; }
        public DateTime? RefundExportedUtc { get; set; }
        public string RefundBatch { get; set; }
        public DateTime? AnonymizedUtc { get; set; }

        public byte[] RowVersion { get; set; }

        public List<ReturnLine> Lines { get; set; } = new List<ReturnLine>();
        public List<ReturnNote> Notes { get; set; } = new List<ReturnNote>();
    }

    public class ReturnLine
    {
        public int Id { get; set; }
        public int ReturnRequestId { get; set; }
        public int OrderLineNumber { get; set; }
        public string Sku { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string ReasonCode { get; set; }
        public string CustomerComment { get; set; }
        public int ReceivedQuantity { get; set; }
        public ItemCondition Condition { get; set; }
        public decimal? RefundAmount { get; set; }
    }

    public class ReturnNote
    {
        public int Id { get; set; }
        public int ReturnRequestId { get; set; }
        public string Author { get; set; }
        public string Text { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public class ScannerDevice
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public string ApiKeyHash { get; set; }
        public bool Enabled { get; set; }
        public DateTime? LastSeenUtc { get; set; }
    }
}

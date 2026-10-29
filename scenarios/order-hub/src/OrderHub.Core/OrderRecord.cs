using System;
using System.Collections.Generic;
using Litware.OrderHub.Contracts;

namespace Litware.OrderHub.Core
{
    public static class OrderStatus
    {
        public const string Rejected = "Rejected";
        public const string Accepted = "Accepted";
        public const string SentToWarehouse = "SentToWarehouse";
        public const string Shipped = "Shipped";
        public const string Invoiced = "Invoiced";
    }

    /// <summary>An order as stored in the hub database.</summary>
    public class OrderRecord
    {
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; }
        public string Channel { get; set; }
        public string ExternalRef { get; set; }
        public string Status { get; set; }
        public string StatusReason { get; set; }
        public DateTime ReceivedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public string WarehouseFile { get; set; }
        public string Carrier { get; set; }
        public string TrackingNumber { get; set; }
        public DateTime? ShippedUtc { get; set; }
        public string InvoiceNumber { get; set; }
        public OrderDocument Document { get; set; }

        public decimal LinesTotal
        {
            get
            {
                decimal total = 0m;
                foreach (var line in Document?.Lines ?? new List<OrderLineDocument>())
                {
                    total += line.Quantity * line.UnitPrice;
                }
                return total;
            }
        }
    }

    public class OrderEvent
    {
        public DateTime AtUtc { get; set; }
        public string EventType { get; set; }
        public string Detail { get; set; }
    }

    public class Product
    {
        public string Sku { get; set; }
        public string Description { get; set; }
        public string Ean { get; set; }
        public bool Active { get; set; }
    }
}

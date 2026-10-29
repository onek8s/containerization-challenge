using System;
using System.Collections.Generic;
using Tailspin.DealerOrders.Contracts.Ax;

namespace Tailspin.DealerOrders.LabStubs
{
    /// <summary>Everything the AX simulator keeps between calls; persisted as JSON when a state file is configured.</summary>
    public class AxSimulatorState
    {
        public Dictionary<string, SimCustomer> Customers { get; set; } = new Dictionary<string, SimCustomer>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, SimDocument> Quotations { get; set; } = new Dictionary<string, SimDocument>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, SimDocument> SalesOrders { get; set; } = new Dictionary<string, SimDocument>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Configurations { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, SimStock> Stock { get; set; } = new Dictionary<string, SimStock>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> NumberSequences { get; set; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        public long Calls { get; set; }

        public long Next(string sequence, long start)
        {
            NumberSequences.TryGetValue(sequence, out var value);
            value = Math.Max(value, start) + 1;
            NumberSequences[sequence] = value;
            return value;
        }
    }

    public class SimCustomer
    {
        public AxCustomer Customer { get; set; }
        public decimal Balance { get; set; }
    }

    public class SimDocument
    {
        public string Id { get; set; }
        public AxSalesDocument Document { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime ModifiedUtc { get; set; }
        public AxSalesStatus Status { get; set; } = AxSalesStatus.Backorder;
        public AxDocumentStatus DocumentStatus { get; set; } = AxDocumentStatus.None;
        public bool InProduction { get; set; }
        public DateTime? ConfirmedDate { get; set; }
        public string QuotationId { get; set; }
        public List<SimLine> Lines { get; set; } = new List<SimLine>();
        public List<AxPackingSlip> PackingSlips { get; set; } = new List<AxPackingSlip>();
        public List<string> InvoiceIds { get; set; } = new List<string>();
    }

    public class SimLine
    {
        public int LineNumber { get; set; }
        public string InventTransId { get; set; }
        public string ItemId { get; set; }
        public string ConfigId { get; set; }
        public decimal Quantity { get; set; }
        public decimal SalesPrice { get; set; }
        public decimal LinePercent { get; set; }
        public decimal Reserved { get; set; }
        public decimal Delivered { get; set; }
        public decimal Invoiced { get; set; }
        public DateTime? ShipDate { get; set; }
        public string SerialId { get; set; }
        public bool Cancelled { get; set; }
        public bool IsMachine { get; set; }

        public decimal NetAmount => Math.Round(SalesPrice * Quantity * (100m - LinePercent) / 100m, 2);
    }

    public class SimStock
    {
        public string ItemId { get; set; }
        public string ConfigId { get; set; }
        public string Warehouse { get; set; }
        public decimal OnHand { get; set; }
        public decimal Reserved { get; set; }
        public decimal OnOrder { get; set; }

        public static string Key(string itemId, string configId, string warehouse) => itemId + "|" + (configId ?? "") + "|" + warehouse;
    }
}

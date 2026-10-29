using System;
using System.Collections.Generic;

namespace VanArsdel.Contracts.Inventory
{
    public class GoodsReceived
    {
        public string ReceiptNo { get; set; }
        public string LocationCode { get; set; }
        public string PurchaseOrderNo { get; set; }
        public DateTime ReceivedUtc { get; set; }
        public List<StockLine> Lines { get; set; } = new List<StockLine>();
    }

    public class TransferShipped
    {
        public string TransferNo { get; set; }
        public string FromLocation { get; set; }
        public string ToLocation { get; set; }
        public List<StockLine> Lines { get; set; } = new List<StockLine>();
    }

    public class ReplenishmentProposed
    {
        public Guid ProposalId { get; set; }
        public string StoreCode { get; set; }
        public string FromLocation { get; set; }
        public DateTime CreatedUtc { get; set; }
        public List<StockLine> Lines { get; set; } = new List<StockLine>();
    }

    public class StockLevelLow
    {
        public string LocationCode { get; set; }
        public string Sku { get; set; }
        public int Available { get; set; }
        public int MinLevel { get; set; }
    }

    public class StockLine
    {
        public string Sku { get; set; }
        public int Quantity { get; set; }
    }
}

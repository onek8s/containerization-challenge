using System;
using System.Collections.Generic;

namespace VanArsdel.Contracts.Pos
{
    /// <summary>
    /// One POS export file (one store, one business day or part of it).
    /// Published by the POS feed service. Kept on the original 2018 shape because the
    /// feed service still runs the first MassTransit version.
    /// </summary>
    public class StoreSalesReceived
    {
        public Guid BatchId { get; set; }
        public string FileName { get; set; }
        public string StoreCode { get; set; }
        public DateTime BusinessDate { get; set; }
        public string Currency { get; set; }
        public int Sequence { get; set; }
        public List<PosReceipt> Receipts { get; set; } = new List<PosReceipt>();
    }

    public class PosReceipt
    {
        public string ReceiptNo { get; set; }
        public string TillNo { get; set; }
        public DateTime Timestamp { get; set; }
        public string CashierId { get; set; }
        public string LoyaltyCardNo { get; set; }
        public bool IsReturn { get; set; }
        public decimal Total { get; set; }
        public decimal VatTotal { get; set; }
        public List<PosLine> Lines { get; set; } = new List<PosLine>();
        public List<PosPayment> Payments { get; set; } = new List<PosPayment>();
    }

    public class PosLine
    {
        public string Sku { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
        public decimal LineTotal { get; set; }
        public string PromotionCode { get; set; }
    }

    public class PosPayment
    {
        public string Tender { get; set; }
        public decimal Amount { get; set; }
    }
}

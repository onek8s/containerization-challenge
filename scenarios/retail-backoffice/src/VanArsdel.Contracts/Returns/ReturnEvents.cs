using System;
using System.Collections.Generic;

namespace VanArsdel.Contracts.Returns
{
    public class ReturnRegistered
    {
        public Guid ReturnId { get; set; }
        public string RmaNo { get; set; }
        public string OrderNo { get; set; }
        public string Channel { get; set; }
        public string CustomerEmail { get; set; }
        public string ReturnLabelUrl { get; set; }
    }

    public class ReturnRestocked
    {
        public Guid ReturnId { get; set; }
        public string RmaNo { get; set; }
        public string LocationCode { get; set; }
        public List<ReturnStockLine> Lines { get; set; } = new List<ReturnStockLine>();
    }

    public class ReturnStockLine
    {
        public string Sku { get; set; }
        public int Quantity { get; set; }
        public string Grade { get; set; }
    }

    public class ReturnCompleted
    {
        public Guid ReturnId { get; set; }
        public string RmaNo { get; set; }
        public string OrderNo { get; set; }
        public string StoreCode { get; set; }
        public string CustomerEmail { get; set; }
        public string LoyaltyCardNo { get; set; }
        public decimal RefundAmount { get; set; }
        public string Currency { get; set; }
        public string RefundMethod { get; set; }
        public DateTime CompletedUtc { get; set; }
        public List<ReturnStockLine> Lines { get; set; } = new List<ReturnStockLine>();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace Wingtip.Invoices.Core.Erp
{
    /// <summary>An approved invoice ready to be transferred to the ERP accounts payable ledger.</summary>
    [Serializable]
    public class PostingInvoice
    {
        public PostingInvoice()
        {
            Lines = new List<PostingLine>();
        }

        public long InvoiceId { get; set; }
        public string Company { get; set; }
        public string VendorNumber { get; set; }
        public string InvoiceNumber { get; set; }
        public int DocumentType { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public string Currency { get; set; }
        public decimal NetAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal GrossAmount { get; set; }
        public string PaymentReference { get; set; }
        public string PurchaseOrderNumber { get; set; }
        public string ReleasedBy { get; set; }
        public List<PostingLine> Lines { get; set; }

        public decimal LinesNetTotal
        {
            get { return Lines.Sum(l => l.NetAmount); }
        }
    }

    [Serializable]
    public class PostingLine
    {
        public int LineNumber { get; set; }
        public string Account { get; set; }
        public string CostCenter { get; set; }
        public decimal Quantity { get; set; }
        public decimal NetAmount { get; set; }
        public decimal VatRate { get; set; }
        public string PurchaseOrderNumber { get; set; }
        public int? PurchaseOrderLine { get; set; }
    }
}

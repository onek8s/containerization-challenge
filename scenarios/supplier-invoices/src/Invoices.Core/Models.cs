using System;
using System.Collections.Generic;
using System.Linq;

namespace Wingtip.Invoices.Core
{
    public enum SourceFormat
    {
        Unknown = 0,
        Edifact = 1,
        Ubl = 2,
        Csv = 3
    }

    public enum InvoiceStatus
    {
        Received = 0,
        Matched = 1,
        Exception = 2,
        Released = 3,
        Posting = 4,
        Posted = 5,
        PostingFailed = 6,
        Rejected = 7
    }

    public enum DocumentType
    {
        Invoice = 380,
        CreditNote = 381
    }

    public class InvoiceDocument
    {
        public InvoiceDocument()
        {
            Lines = new List<InvoiceLine>();
            Currency = "EUR";
            Type = DocumentType.Invoice;
        }

        public DocumentType Type { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string SupplierVatNumber { get; set; }
        public string SupplierName { get; set; }
        public string BuyerReference { get; set; }
        public string PurchaseOrderNumber { get; set; }
        public string Currency { get; set; }
        public decimal NetAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal GrossAmount { get; set; }
        public string PaymentReference { get; set; }
        public IList<InvoiceLine> Lines { get; private set; }

        public decimal LinesNetTotal
        {
            get { return Lines.Sum(l => l.NetAmount); }
        }

        public decimal LinesVatTotal
        {
            get { return Lines.Sum(l => Money.Round(l.NetAmount * l.VatRate / 100m)); }
        }
    }

    public class InvoiceLine
    {
        public int LineNumber { get; set; }
        public string ItemNumber { get; set; }
        public string SupplierItemNumber { get; set; }
        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal NetAmount { get; set; }
        public decimal VatRate { get; set; }
        public string PurchaseOrderNumber { get; set; }
        public int? PurchaseOrderLine { get; set; }
    }

    public static class Money
    {
        public static decimal Round(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }
    }
}

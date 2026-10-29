using System;
using System.Collections.Generic;

namespace Wingtip.Invoices.Core.Matching
{
    [Serializable]
    public class PurchaseOrder
    {
        public PurchaseOrder()
        {
            Lines = new List<PurchaseOrderLine>();
        }

        public string Number { get; set; }
        public string VendorNumber { get; set; }
        public string Currency { get; set; }
        public string Buyer { get; set; }
        public string CostCenter { get; set; }
        public bool Closed { get; set; }
        public List<PurchaseOrderLine> Lines { get; set; }
    }

    [Serializable]
    public class PurchaseOrderLine
    {
        public int LineNumber { get; set; }
        public string ItemNumber { get; set; }
        public string SupplierItemNumber { get; set; }
        public string Description { get; set; }
        public decimal OrderedQuantity { get; set; }
        public decimal ReceivedQuantity { get; set; }
        public decimal InvoicedQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string Account { get; set; }

        /// <summary>Service and expense lines are not received in the warehouse.</summary>
        public bool ReceiptRequired { get; set; }
    }

    [Serializable]
    public class Vendor
    {
        public string Number { get; set; }
        public string Name { get; set; }
        public string VatNumber { get; set; }
        public string Currency { get; set; }
        public string PaymentTerms { get; set; }
        public bool Blocked { get; set; }
    }
}

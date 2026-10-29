using System;
using System.IO;
using System.Text;
using Wingtip.Invoices.Core.Matching;

namespace Wingtip.Invoices.Core.Tests
{
    internal static class Samples
    {
        static Samples()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public static byte[] Bytes(string name)
        {
            return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", name));
        }

        public static string Text(string name)
        {
            return Parsing.InvoiceParsers.Decode(Bytes(name), Encoding.GetEncoding(1252));
        }

        public static Vendor NordicFasteners()
        {
            return new Vendor { Number = "V10021", Name = "Nordic Fasteners Oy", VatNumber = "FI12345671", Currency = "EUR", PaymentTerms = "N30" };
        }

        public static PurchaseOrder FastenerOrder()
        {
            var po = new PurchaseOrder { Number = "4500012001", VendorNumber = "V10021", Currency = "EUR", CostCenter = "WH01" };
            po.Lines.Add(new PurchaseOrderLine { LineNumber = 1, ItemNumber = "100-2040", SupplierItemNumber = "NF-M840Z", OrderedQuantity = 2000, ReceivedQuantity = 2000, UnitPrice = 0.085m, Account = "14100", ReceiptRequired = true });
            po.Lines.Add(new PurchaseOrderLine { LineNumber = 2, ItemNumber = "100-2050", SupplierItemNumber = "NF-M8N", OrderedQuantity = 2000, ReceivedQuantity = 1500, UnitPrice = 0.032m, Account = "14100", ReceiptRequired = true });
            return po;
        }
    }
}

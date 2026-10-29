using System.Linq;
using Wingtip.Invoices.Core;
using Wingtip.Invoices.Core.Erp;
using Wingtip.Invoices.Core.Matching;
using Wingtip.Invoices.Infrastructure.Configuration;
using Wingtip.Invoices.Infrastructure.Data;

namespace Wingtip.Invoices.Processor
{
    public static class PostingBuilder
    {
        public static PostingInvoice Build(StoredInvoice stored, SupplierInfo supplier, Vendor vendor, string releasedBy)
        {
            var doc = stored.Document;
            var defaultAccount = HubSettings.Get("DefaultExpenseAccount", "40000");
            var posting = new PostingInvoice
            {
                InvoiceId = stored.InvoiceId,
                Company = HubSettings.Company,
                VendorNumber = stored.VendorNumber,
                InvoiceNumber = doc.InvoiceNumber,
                DocumentType = (int)doc.Type,
                InvoiceDate = doc.InvoiceDate,
                DueDate = PaymentTerms.DueDate(doc.InvoiceDate, vendor == null ? null : vendor.PaymentTerms, doc.DueDate),
                Currency = doc.Currency,
                NetAmount = doc.NetAmount,
                VatAmount = doc.VatAmount,
                GrossAmount = doc.GrossAmount,
                PaymentReference = doc.PaymentReference,
                PurchaseOrderNumber = doc.PurchaseOrderNumber,
                ReleasedBy = releasedBy
            };

            posting.Lines.AddRange(doc.Lines.Select(l =>
            {
                string account;
                stored.LineAccounts.TryGetValue(l.LineNumber, out account);
                return new PostingLine
                {
                    LineNumber = l.LineNumber,
                    Account = string.IsNullOrEmpty(account) ? defaultAccount : account,
                    CostCenter = supplier.CostCenter,
                    Quantity = l.Quantity,
                    NetAmount = l.NetAmount,
                    VatRate = l.VatRate,
                    PurchaseOrderNumber = l.PurchaseOrderNumber,
                    PurchaseOrderLine = l.PurchaseOrderLine
                };
            }));

            // Header net amounts within the rounding tolerance are posted as the sum of the lines.
            posting.NetAmount = posting.LinesNetTotal;
            return posting;
        }
    }
}

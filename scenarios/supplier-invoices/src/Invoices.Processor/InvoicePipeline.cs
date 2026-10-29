using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Messaging;
using System.Text;
using Wingtip.Invoices.Core;
using Wingtip.Invoices.Core.Erp;
using Wingtip.Invoices.Core.Matching;
using Wingtip.Invoices.Core.Parsing;
using Wingtip.Invoices.Infrastructure.Configuration;
using Wingtip.Invoices.Infrastructure.Data;
using Wingtip.Invoices.Infrastructure.Diagnostics;
using Wingtip.Invoices.Infrastructure.Messaging;

namespace Wingtip.Invoices.Processor
{
    /// <summary>Parses, validates and matches supplier files and hands approved invoices to posting.</summary>
    public class InvoicePipeline
    {
        public const string SystemUser = "InvoiceProcessor";

        private readonly FileRepository files = new FileRepository();
        private readonly InvoiceRepository invoices = new InvoiceRepository();
        private readonly ErpLookupClient erp;
        private readonly HubCounters counters;

        public InvoicePipeline(ErpLookupClient erp, HubCounters counters)
        {
            this.erp = erp;
            this.counters = counters;
        }

        public void Process(InboundFileMessage message, MessageQueue postingQueue, MessageQueueTransaction transaction)
        {
            var supplier = invoices.GetSupplier(message.SupplierId);
            if (supplier == null) throw new InvalidOperationException("Supplier " + message.SupplierId + " does not exist.");

            var content = message.Content ?? File.ReadAllBytes(message.ArchivePath);
            var text = InvoiceParsers.Decode(content, Encoding.GetEncoding(supplier.CodePage));
            var format = InvoiceParsers.Detect(message.FileName, text);
            if (format == SourceFormat.Unknown)
            {
                Reject(message, "The file format was not recognised.");
                return;
            }

            IList<InvoiceDocument> documents;
            try
            {
                documents = InvoiceParsers.For(format).Parse(text);
            }
            catch (InvoiceParseException ex)
            {
                Reject(message, format + ": " + ex.Message);
                return;
            }

            var vendor = erp.FindVendor(documents[0].SupplierVatNumber ?? supplier.VatNumber, supplier.VendorNumber);
            var validator = new InvoiceValidator { MaxAgeDays = HubSettings.GetInt("MaxInvoiceAgeDays", 365) };
            var matcher = new InvoiceMatcher(Policy(supplier));
            var periodStart = erp.GetOpenPeriodStart();

            foreach (var doc in documents)
            {
                var issues = validator.Validate(doc, DateTime.Today).ToList();
                if (vendor != null && doc.SupplierVatNumber != null
                    && !string.Equals(VatNumber.Normalize(doc.SupplierVatNumber), VatNumber.Normalize(vendor.VatNumber), StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new Issue(IssueCodes.SupplierVat, "Invoice VAT number " + doc.SupplierVatNumber + " differs from vendor " + vendor.Number + " (" + vendor.VatNumber + ")."));
                }

                var order = LoadOrder(doc, message.SupplierId);
                var match = matcher.Match(doc, vendor, order);
                if (doc.InvoiceDate < periodStart)
                {
                    match.Issues.Add(new Issue(IssueCodes.PeriodClosed, "Invoice date is in a closed period; accounts payable must choose the posting date."));
                }

                var saved = invoices.Save(message.FileId, message.SupplierId, vendor == null ? supplier.VendorNumber : vendor.Number, doc, match, issues);
                counters.InvoiceProcessed(saved.Status != InvoiceStatus.Matched);

                switch (saved.Outcome)
                {
                    case "Duplicate":
                        HubLog.Warning(HubLog.Events.FileRejected, "Invoice {0} of {1} in file {2} has already been received in another file.", doc.InvoiceNumber, supplier.Code, message.FileId);
                        continue;
                    case "Exists":
                        // Processed before, e.g. the service stopped before the queue transaction committed.
                        if (saved.Status == InvoiceStatus.Released) SendPosting(saved.InvoiceId, supplier, vendor, SystemUser, postingQueue, transaction);
                        continue;
                }

                if (saved.Status == InvoiceStatus.Matched && invoices.MarkReleased(saved.InvoiceId, SystemUser, "Matched automatically"))
                {
                    SendPosting(saved.InvoiceId, supplier, vendor, SystemUser, postingQueue, transaction);
                }

                HubLog.Info(HubLog.Events.InvoiceProcessed, "Invoice {0} ({1}) of {2}: {3}.", saved.InvoiceId, doc.InvoiceNumber, supplier.Code, match.Summary);
            }

            files.SetStatus(message.FileId, FileStatus.Processed, null);
        }

        public MatchResult Rematch(long invoiceId, string user)
        {
            var stored = Load(invoiceId);
            if (stored.Status != InvoiceStatus.Exception && stored.Status != InvoiceStatus.Matched)
                throw new InvalidOperationException("Only invoices waiting in accounts payable can be matched again.");

            var supplier = invoices.GetSupplier(stored.SupplierId);
            var vendor = erp.FindVendor(stored.Document.SupplierVatNumber, supplier.VendorNumber);
            var order = LoadOrder(stored.Document, stored.SupplierId);
            var match = new InvoiceMatcher(Policy(supplier)).Match(stored.Document, vendor, order);

            invoices.UpdateMatch(invoiceId, match, user);
            return match;
        }

        public void Release(long invoiceId, string user, string comment, MessageQueue postingQueue)
        {
            var stored = Load(invoiceId);
            if (!invoices.MarkReleased(invoiceId, user, comment))
                throw new InvalidOperationException("Invoice " + invoiceId + " cannot be released in its current status.");

            var supplier = invoices.GetSupplier(stored.SupplierId);
            var vendor = erp.GetVendor(stored.VendorNumber);
            using (var tx = new MessageQueueTransaction())
            {
                tx.Begin();
                SendPosting(invoiceId, supplier, vendor, user, postingQueue, tx);
                tx.Commit();
            }
        }

        private void SendPosting(long invoiceId, SupplierInfo supplier, Vendor vendor, string user, MessageQueue postingQueue, MessageQueueTransaction transaction)
        {
            var stored = Load(invoiceId);
            PostingInvoice posting = PostingBuilder.Build(stored, supplier, vendor, user);
            HubQueues.Send(postingQueue, posting, "Invoice " + invoiceId + " " + supplier.Code + " " + stored.Document.InvoiceNumber, transaction);
        }

        private StoredInvoice Load(long invoiceId)
        {
            var stored = invoices.Load(invoiceId);
            if (stored == null) throw new InvalidOperationException("Invoice " + invoiceId + " does not exist.");
            return stored;
        }

        private PurchaseOrder LoadOrder(InvoiceDocument doc, int supplierId)
        {
            var number = doc.PurchaseOrderNumber ?? doc.Lines.Select(l => l.PurchaseOrderNumber).FirstOrDefault(n => !string.IsNullOrEmpty(n));
            var order = erp.GetPurchaseOrder(number);
            if (order == null) return null;

            // The ERP only knows posted invoices; add the ones still on their way through the hub.
            var pending = invoices.GetPendingQuantities(order.Number, supplierId, doc.InvoiceNumber);
            foreach (var line in order.Lines)
            {
                decimal quantity;
                if (pending.TryGetValue(line.LineNumber, out quantity)) line.InvoicedQuantity += quantity;
            }
            return order;
        }

        private void Reject(InboundFileMessage message, string reason)
        {
            files.SetStatus(message.FileId, FileStatus.Failed, reason);
            HubLog.Warning(HubLog.Events.FileRejected, "File {0} {1} from {2} was rejected: {3}", message.FileId, message.FileName, message.SupplierCode, reason);
        }

        private static MatchingPolicy Policy(SupplierInfo supplier)
        {
            return new MatchingPolicy
            {
                PriceTolerancePercent = supplier.PriceTolerancePercent ?? HubSettings.GetDecimal("PriceTolerancePercent", 2m),
                TotalToleranceAmount = HubSettings.GetDecimal("TotalToleranceAmount", 5m),
                AutoApproveLimit = HubSettings.GetDecimal("AutoApproveLimit", 25000m),
                TwoWayMatch = supplier.TwoWayMatch
            };
        }
    }
}

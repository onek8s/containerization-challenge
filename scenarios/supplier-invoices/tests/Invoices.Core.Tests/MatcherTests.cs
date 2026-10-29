using System.Linq;
using Wingtip.Invoices.Core.Matching;
using Wingtip.Invoices.Core.Parsing;
using Xunit;

namespace Wingtip.Invoices.Core.Tests
{
    public class MatcherTests
    {
        private static InvoiceDocument FastenerInvoice()
        {
            return new EdifactInvoiceParser().Parse(Samples.Text("NF_INVOIC_9100231.edi")).Single();
        }

        [Fact]
        public void Sample_invoice_matches_order_and_receipts()
        {
            var result = new InvoiceMatcher(new MatchingPolicy()).Match(FastenerInvoice(), Samples.NordicFasteners(), Samples.FastenerOrder());

            Assert.True(result.Matched, result.Summary);
            Assert.Equal(InvoiceStatus.Matched, result.Status);
            Assert.Equal(218.00m, result.ExpectedNetAmount);
            Assert.All(result.Lines, l => Assert.Equal("14100", l.Account));
        }

        [Fact]
        public void Quantity_above_receipt_is_an_exception_unless_two_way()
        {
            var order = Samples.FastenerOrder();
            order.Lines[1].ReceivedQuantity = 1000;

            var three = new InvoiceMatcher(new MatchingPolicy()).Match(FastenerInvoice(), Samples.NordicFasteners(), order);
            Assert.Contains(three.Issues, i => i.Code == IssueCodes.QuantityNotReceived);

            var two = new InvoiceMatcher(new MatchingPolicy { TwoWayMatch = true }).Match(FastenerInvoice(), Samples.NordicFasteners(), order);
            Assert.True(two.Matched, two.Summary);
        }

        [Fact]
        public void Earlier_invoices_count_against_the_order()
        {
            var order = Samples.FastenerOrder();
            order.Lines[0].InvoicedQuantity = 500;
            var result = new InvoiceMatcher(new MatchingPolicy()).Match(FastenerInvoice(), Samples.NordicFasteners(), order);
            Assert.Contains(result.Issues, i => i.Code == IssueCodes.QuantityExceedsOrder);
        }

        [Fact]
        public void Price_above_tolerance_is_an_exception()
        {
            var order = Samples.FastenerOrder();
            order.Lines[0].UnitPrice = 0.083m; // invoice 0.085 = +2.4 %
            var result = new InvoiceMatcher(new MatchingPolicy()).Match(FastenerInvoice(), Samples.NordicFasteners(), order);
            Assert.Contains(result.Issues, i => i.Code == IssueCodes.PriceVariance);

            var lenient = new InvoiceMatcher(new MatchingPolicy { PriceTolerancePercent = 3m, TotalToleranceAmount = 10m }).Match(FastenerInvoice(), Samples.NordicFasteners(), order);
            Assert.True(lenient.Matched, lenient.Summary);
        }

        [Fact]
        public void Lines_are_found_by_supplier_item_number()
        {
            var inv = FastenerInvoice();
            foreach (var l in inv.Lines) { l.PurchaseOrderLine = null; l.ItemNumber = null; }
            var result = new InvoiceMatcher(new MatchingPolicy()).Match(inv, Samples.NordicFasteners(), Samples.FastenerOrder());
            Assert.Equal(new int?[] { 1, 2 }, result.Lines.Select(l => l.OrderLine));
        }

        [Fact]
        public void Unknown_vendor_missing_or_foreign_order()
        {
            var matcher = new InvoiceMatcher(new MatchingPolicy());
            Assert.Equal(IssueCodes.SupplierUnknown, matcher.Match(FastenerInvoice(), null, null).Issues.Single().Code);
            Assert.Equal(IssueCodes.PurchaseOrderNotFound, matcher.Match(FastenerInvoice(), Samples.NordicFasteners(), null).Issues.Single().Code);

            var order = Samples.FastenerOrder();
            order.VendorNumber = "V99999";
            order.Closed = true;
            var codes = matcher.Match(FastenerInvoice(), Samples.NordicFasteners(), order).Issues.Select(i => i.Code).ToList();
            Assert.Contains(IssueCodes.SupplierMismatch, codes);
            Assert.Contains(IssueCodes.PurchaseOrderClosed, codes);

            var noPo = FastenerInvoice();
            noPo.PurchaseOrderNumber = null;
            foreach (var l in noPo.Lines) l.PurchaseOrderNumber = null;
            Assert.Equal(IssueCodes.PurchaseOrderMissing, matcher.Match(noPo, Samples.NordicFasteners(), order).Issues.Single().Code);
        }

        [Fact]
        public void Large_invoices_need_release()
        {
            var result = new InvoiceMatcher(new MatchingPolicy { AutoApproveLimit = 200m }).Match(FastenerInvoice(), Samples.NordicFasteners(), Samples.FastenerOrder());
            Assert.Equal(IssueCodes.ApprovalLimit, result.Issues.Single().Code);
        }

        [Fact]
        public void Credit_notes_go_to_review()
        {
            var inv = FastenerInvoice();
            inv.Type = DocumentType.CreditNote;
            var result = new InvoiceMatcher(new MatchingPolicy()).Match(inv, Samples.NordicFasteners(), Samples.FastenerOrder());
            Assert.Contains(result.Issues, i => i.Code == IssueCodes.CreditNote);
        }
    }
}

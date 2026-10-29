using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Wingtip.Invoices.Core.Matching
{
    /// <summary>
    /// Matches invoice lines against purchase order lines and goods receipts (three-way match).
    /// </summary>
    public class InvoiceMatcher
    {
        private readonly MatchingPolicy policy;

        public InvoiceMatcher(MatchingPolicy policy)
        {
            this.policy = policy ?? new MatchingPolicy();
        }

        public MatchResult Match(InvoiceDocument invoice, Vendor vendor, PurchaseOrder order)
        {
            var result = new MatchResult();

            if (vendor == null)
            {
                result.Issues.Add(new Issue(IssueCodes.SupplierUnknown, "No vendor in the ERP with VAT number " + invoice.SupplierVatNumber + "."));
                return result;
            }

            if (invoice.Type == DocumentType.CreditNote)
                result.Issues.Add(new Issue(IssueCodes.CreditNote, "Credit notes are checked by accounts payable."));

            if (string.IsNullOrEmpty(invoice.PurchaseOrderNumber) && invoice.Lines.All(l => string.IsNullOrEmpty(l.PurchaseOrderNumber)))
            {
                result.Issues.Add(new Issue(IssueCodes.PurchaseOrderMissing, "The invoice has no purchase order reference."));
                return result;
            }

            if (order == null)
            {
                result.Issues.Add(new Issue(IssueCodes.PurchaseOrderNotFound, "Purchase order " + invoice.PurchaseOrderNumber + " was not found."));
                return result;
            }

            if (order.Closed)
                result.Issues.Add(new Issue(IssueCodes.PurchaseOrderClosed, "Purchase order " + order.Number + " is closed."));
            if (!string.Equals(order.VendorNumber, vendor.Number, StringComparison.OrdinalIgnoreCase))
                result.Issues.Add(new Issue(IssueCodes.SupplierMismatch, "Purchase order " + order.Number + " belongs to vendor " + order.VendorNumber + "."));
            if (!string.Equals(order.Currency, invoice.Currency, StringComparison.OrdinalIgnoreCase))
                result.Issues.Add(new Issue(IssueCodes.CurrencyMismatch, "Order currency " + order.Currency + ", invoice currency " + invoice.Currency + "."));

            var used = new Dictionary<int, decimal>();
            foreach (var line in invoice.Lines)
            {
                var orderLine = FindLine(order, line);
                if (orderLine == null)
                {
                    result.Issues.Add(new Issue(IssueCodes.LineNotOnOrder, string.Format(CultureInfo.InvariantCulture,
                        "Line {0} ({1}) is not on purchase order {2}.", line.LineNumber, line.ItemNumber ?? line.SupplierItemNumber ?? line.Description, order.Number)));
                    result.Lines.Add(new LineMatch { InvoiceLine = line.LineNumber, ExpectedAmount = line.NetAmount });
                    continue;
                }

                decimal earlier;
                used.TryGetValue(orderLine.LineNumber, out earlier);
                used[orderLine.LineNumber] = earlier + line.Quantity;
                var invoicedSoFar = orderLine.InvoicedQuantity + earlier + line.Quantity;

                if (invoicedSoFar > orderLine.OrderedQuantity)
                {
                    result.Issues.Add(new Issue(IssueCodes.QuantityExceedsOrder, string.Format(CultureInfo.InvariantCulture,
                        "Line {0}: invoiced {1} in total, ordered {2}.", line.LineNumber, invoicedSoFar, orderLine.OrderedQuantity)));
                }
                else if (orderLine.ReceiptRequired && !policy.TwoWayMatch && invoicedSoFar > orderLine.ReceivedQuantity)
                {
                    result.Issues.Add(new Issue(IssueCodes.QuantityNotReceived, string.Format(CultureInfo.InvariantCulture,
                        "Line {0}: invoiced {1} in total, received {2}.", line.LineNumber, invoicedSoFar, orderLine.ReceivedQuantity)));
                }

                if (orderLine.UnitPrice != 0m)
                {
                    var variance = (line.UnitPrice - orderLine.UnitPrice) / orderLine.UnitPrice * 100m;
                    if (variance > policy.PriceTolerancePercent)
                        result.Issues.Add(new Issue(IssueCodes.PriceVariance, string.Format(CultureInfo.InvariantCulture,
                            "Line {0}: price {1}, order price {2} ({3:0.0} %).", line.LineNumber, line.UnitPrice, orderLine.UnitPrice, variance)));
                }

                var expected = Money.Round(line.Quantity * orderLine.UnitPrice);
                result.Lines.Add(new LineMatch
                {
                    InvoiceLine = line.LineNumber,
                    OrderLine = orderLine.LineNumber,
                    ExpectedAmount = expected,
                    Account = orderLine.Account
                });
            }

            result.ExpectedNetAmount = result.Lines.Sum(l => l.ExpectedAmount);
            if (invoice.NetAmount - result.ExpectedNetAmount > policy.TotalToleranceAmount)
                result.Issues.Add(new Issue(IssueCodes.TotalVariance, string.Format(CultureInfo.InvariantCulture,
                    "Invoice net {0}, expected {1} from the order.", invoice.NetAmount, result.ExpectedNetAmount)));

            if (result.Issues.Count == 0 && invoice.NetAmount > policy.AutoApproveLimit)
                result.Issues.Add(new Issue(IssueCodes.ApprovalLimit, string.Format(CultureInfo.InvariantCulture,
                    "Net amount {0} is above the automatic approval limit {1}.", invoice.NetAmount, policy.AutoApproveLimit)));

            return result;
        }

        private static PurchaseOrderLine FindLine(PurchaseOrder order, InvoiceLine line)
        {
            if (line.PurchaseOrderLine.HasValue)
            {
                var byNumber = order.Lines.FirstOrDefault(l => l.LineNumber == line.PurchaseOrderLine.Value);
                if (byNumber != null) return byNumber;
            }
            if (!string.IsNullOrEmpty(line.ItemNumber))
            {
                var byItem = order.Lines.FirstOrDefault(l => string.Equals(l.ItemNumber, line.ItemNumber, StringComparison.OrdinalIgnoreCase));
                if (byItem != null) return byItem;
            }
            if (!string.IsNullOrEmpty(line.SupplierItemNumber))
            {
                return order.Lines.FirstOrDefault(l => string.Equals(l.SupplierItemNumber, line.SupplierItemNumber, StringComparison.OrdinalIgnoreCase));
            }
            return null;
        }
    }
}

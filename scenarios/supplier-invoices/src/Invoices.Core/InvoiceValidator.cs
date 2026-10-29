using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Wingtip.Invoices.Core
{
    public class Issue
    {
        public Issue(string code, string message)
        {
            Code = code;
            Message = message;
        }

        public string Code { get; private set; }
        public string Message { get; private set; }

        public override string ToString()
        {
            return Code + ": " + Message;
        }
    }

    public static class IssueCodes
    {
        public const string MissingField = "MISSING_FIELD";
        public const string FutureDate = "FUTURE_DATE";
        public const string OldInvoice = "OLD_INVOICE";
        public const string LineTotal = "LINE_TOTAL";
        public const string HeaderTotal = "HEADER_TOTAL";
        public const string VatTotal = "VAT_TOTAL";
        public const string VatRate = "VAT_RATE";
        public const string NoLines = "NO_LINES";
        public const string SupplierUnknown = "SUPPLIER_UNKNOWN";
        public const string SupplierVat = "SUPPLIER_VAT";
        public const string PurchaseOrderMissing = "PO_MISSING";
        public const string PurchaseOrderNotFound = "PO_NOT_FOUND";
        public const string PurchaseOrderClosed = "PO_CLOSED";
        public const string SupplierMismatch = "SUPPLIER_MISMATCH";
        public const string CurrencyMismatch = "CURRENCY_MISMATCH";
        public const string LineNotOnOrder = "LINE_NOT_ON_PO";
        public const string PriceVariance = "PRICE_VARIANCE";
        public const string QuantityNotReceived = "QTY_NOT_RECEIVED";
        public const string QuantityExceedsOrder = "QTY_EXCEEDS_PO";
        public const string TotalVariance = "TOTAL_VARIANCE";
        public const string CreditNote = "CREDIT_NOTE";
        public const string ApprovalLimit = "APPROVAL_LIMIT";
        public const string PeriodClosed = "PERIOD_CLOSED";
    }

    public class InvoiceValidator
    {
        private static readonly decimal[] KnownVatRates = { 0m, 10m, 14m, 24m, 25.5m };

        public InvoiceValidator()
        {
            RoundingTolerance = 0.05m;
            MaxAgeDays = 365;
        }

        public decimal RoundingTolerance { get; set; }
        public int MaxAgeDays { get; set; }

        public IList<Issue> Validate(InvoiceDocument invoice, DateTime today)
        {
            var issues = new List<Issue>();

            if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
                issues.Add(new Issue(IssueCodes.MissingField, "Invoice number is missing."));
            if (invoice.InvoiceDate == default(DateTime))
                issues.Add(new Issue(IssueCodes.MissingField, "Invoice date is missing."));
            else if (invoice.InvoiceDate.Date > today.Date.AddDays(1))
                issues.Add(new Issue(IssueCodes.FutureDate, "Invoice date " + invoice.InvoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " is in the future."));
            else if (invoice.InvoiceDate.Date < today.Date.AddDays(-MaxAgeDays))
                issues.Add(new Issue(IssueCodes.OldInvoice, "Invoice is older than " + MaxAgeDays + " days."));

            if (string.IsNullOrWhiteSpace(invoice.SupplierVatNumber))
                issues.Add(new Issue(IssueCodes.MissingField, "Supplier VAT number is missing."));

            if (invoice.Lines.Count == 0)
            {
                issues.Add(new Issue(IssueCodes.NoLines, "Invoice has no lines."));
                return issues;
            }

            foreach (var line in invoice.Lines)
            {
                var expected = Money.Round(line.Quantity * line.UnitPrice);
                if (Math.Abs(expected - line.NetAmount) > RoundingTolerance)
                    issues.Add(new Issue(IssueCodes.LineTotal, string.Format(CultureInfo.InvariantCulture,
                        "Line {0}: {1} x {2} = {3}, invoiced {4}.", line.LineNumber, line.Quantity, line.UnitPrice, expected, line.NetAmount)));
                if (!KnownVatRates.Contains(line.VatRate))
                    issues.Add(new Issue(IssueCodes.VatRate, string.Format(CultureInfo.InvariantCulture,
                        "Line {0}: VAT rate {1} % is not valid.", line.LineNumber, line.VatRate)));
            }

            if (Math.Abs(invoice.LinesNetTotal - invoice.NetAmount) > RoundingTolerance)
                issues.Add(new Issue(IssueCodes.HeaderTotal, string.Format(CultureInfo.InvariantCulture,
                    "Lines total {0}, invoice net amount {1}.", invoice.LinesNetTotal, invoice.NetAmount)));

            // VAT is calculated per rate on the rate subtotal, not per line.
            var vat = invoice.Lines.GroupBy(l => l.VatRate).Sum(g => Money.Round(g.Sum(l => l.NetAmount) * g.Key / 100m));
            if (Math.Abs(vat - invoice.VatAmount) > RoundingTolerance)
                issues.Add(new Issue(IssueCodes.VatTotal, string.Format(CultureInfo.InvariantCulture,
                    "Calculated VAT {0}, invoiced VAT {1}.", vat, invoice.VatAmount)));

            if (Math.Abs(invoice.NetAmount + invoice.VatAmount - invoice.GrossAmount) > RoundingTolerance)
                issues.Add(new Issue(IssueCodes.HeaderTotal, string.Format(CultureInfo.InvariantCulture,
                    "Net {0} + VAT {1} does not equal gross {2}.", invoice.NetAmount, invoice.VatAmount, invoice.GrossAmount)));

            return issues;
        }
    }
}

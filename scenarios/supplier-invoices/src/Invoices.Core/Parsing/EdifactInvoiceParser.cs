using System;
using System.Collections.Generic;
using System.Globalization;

namespace Wingtip.Invoices.Core.Parsing
{
    /// <summary>
    /// UN/EDIFACT INVOIC (D.96A, EANCOM subset used by our suppliers).
    /// </summary>
    public class EdifactInvoiceParser : IInvoiceParser
    {
        public SourceFormat Format
        {
            get { return SourceFormat.Edifact; }
        }

        public IList<InvoiceDocument> Parse(string content)
        {
            var segments = EdifactReader.Read(content);
            var invoices = new List<InvoiceDocument>();
            InvoiceDocument invoice = null;
            InvoiceLine line = null;
            string lastNad = null;
            var inSummary = false;
            var messageSegments = 0;

            foreach (var s in segments)
            {
                if (invoice != null) messageSegments++;

                switch (s.Tag)
                {
                    case "UNB":
                    case "UNZ":
                        break;

                    case "UNH":
                        if (s.Get(1, 0) != "INVOIC")
                            throw new InvoiceParseException("Unsupported message type " + s.Get(1, 0) + ".");
                        invoice = new InvoiceDocument();
                        line = null;
                        lastNad = null;
                        inSummary = false;
                        messageSegments = 1;
                        break;

                    case "BGM":
                        RequireMessage(invoice, s);
                        var code = s.Get(0, 0);
                        if (code == "381") invoice.Type = DocumentType.CreditNote;
                        else if (code == "380" || code == null) invoice.Type = DocumentType.Invoice;
                        else throw new InvoiceParseException("Unsupported document name code " + code + ".");
                        invoice.InvoiceNumber = s.Get(1, 0);
                        break;

                    case "DTM":
                        RequireMessage(invoice, s);
                        var qualifier = s.Get(0, 0);
                        var date = ParseDate(s.Get(0, 1), s.Get(0, 2));
                        if (qualifier == "137") invoice.InvoiceDate = date;
                        else if (qualifier == "13") invoice.DueDate = date;
                        break;

                    case "RFF":
                        RequireMessage(invoice, s);
                        ReadReference(invoice, line, lastNad, s);
                        break;

                    case "NAD":
                        RequireMessage(invoice, s);
                        lastNad = s.Get(0, 0);
                        if (lastNad == "SU") invoice.SupplierName = s.Get(3, 0) ?? s.Get(2, 0);
                        break;

                    case "CUX":
                        RequireMessage(invoice, s);
                        invoice.Currency = s.Get(0, 1) ?? invoice.Currency;
                        break;

                    case "LIN":
                        RequireMessage(invoice, s);
                        line = new InvoiceLine
                        {
                            LineNumber = ParseInt(s.Get(0, 0), s),
                            ItemNumber = s.Get(2, 0)
                        };
                        invoice.Lines.Add(line);
                        lastNad = null;
                        break;

                    case "PIA":
                        if (line != null && s.Get(1, 1) == "SA") line.SupplierItemNumber = s.Get(1, 0);
                        break;

                    case "IMD":
                        if (line != null) line.Description = s.Get(2, 3);
                        break;

                    case "QTY":
                        if (line != null && s.Get(0, 0) == "47")
                        {
                            line.Quantity = ParseDecimal(s.Get(0, 1), s);
                            line.Unit = s.Get(0, 2) ?? "PCE";
                        }
                        break;

                    case "PRI":
                        if (line != null && (s.Get(0, 0) == "AAA" || s.Get(0, 0) == "AAB"))
                        {
                            var price = ParseDecimal(s.Get(0, 1), s);
                            var basis = s.Get(0, 4);
                            line.UnitPrice = basis == null ? price : price / ParseDecimal(basis, s);
                        }
                        break;

                    case "TAX":
                        var rate = s.Get(4, 3);
                        if (rate == null) break;
                        if (line != null && !inSummary) line.VatRate = ParseDecimal(rate, s);
                        break;

                    case "MOA":
                        RequireMessage(invoice, s);
                        ReadAmount(invoice, inSummary ? null : line, s);
                        break;

                    case "UNS":
                        inSummary = true;
                        line = null;
                        break;

                    case "UNT":
                        RequireMessage(invoice, s);
                        var declared = ParseInt(s.Get(0, 0), s);
                        if (declared != messageSegments)
                            throw new InvoiceParseException(string.Format(CultureInfo.InvariantCulture,
                                "Message {0} declares {1} segments but contains {2}.", invoice.InvoiceNumber, declared, messageSegments));
                        Complete(invoice);
                        invoices.Add(invoice);
                        invoice = null;
                        break;
                }
            }

            if (invoice != null) throw new InvoiceParseException("Message without UNT trailer.");
            if (invoices.Count == 0) throw new InvoiceParseException("The interchange contains no INVOIC messages.");
            return invoices;
        }

        private static void ReadReference(InvoiceDocument invoice, InvoiceLine line, string lastNad, EdifactSegment s)
        {
            var qualifier = s.Get(0, 0);
            var value = s.Get(0, 1);
            switch (qualifier)
            {
                case "ON":
                    if (line != null)
                    {
                        line.PurchaseOrderNumber = value;
                        var poLine = s.Get(0, 2);
                        if (poLine != null) line.PurchaseOrderLine = ParseInt(poLine, s);
                    }
                    else invoice.PurchaseOrderNumber = value;
                    break;
                case "LI":
                    if (line != null) line.PurchaseOrderLine = ParseInt(value, s);
                    break;
                case "VA":
                    if (lastNad == "SU") invoice.SupplierVatNumber = value;
                    break;
                case "PQ":
                    invoice.PaymentReference = value;
                    break;
                case "CR":
                    invoice.BuyerReference = value;
                    break;
            }
        }

        private static void ReadAmount(InvoiceDocument invoice, InvoiceLine line, EdifactSegment s)
        {
            var qualifier = s.Get(0, 0);
            var amount = ParseDecimal(s.Get(0, 1), s);
            if (line != null)
            {
                if (qualifier == "203") line.NetAmount = amount;
                return;
            }

            switch (qualifier)
            {
                case "77":
                    invoice.GrossAmount = amount;
                    break;
                case "79":
                case "125":
                    invoice.NetAmount = amount;
                    break;
                case "124":
                case "176":
                    invoice.VatAmount = amount;
                    break;
            }
        }

        private static void Complete(InvoiceDocument invoice)
        {
            foreach (var line in invoice.Lines)
            {
                if (line.NetAmount == 0m) line.NetAmount = Money.Round(line.Quantity * line.UnitPrice);
                if (line.PurchaseOrderNumber == null) line.PurchaseOrderNumber = invoice.PurchaseOrderNumber;
            }
            if (invoice.NetAmount == 0m) invoice.NetAmount = invoice.LinesNetTotal;
            if (invoice.GrossAmount == 0m) invoice.GrossAmount = invoice.NetAmount + invoice.VatAmount;
        }

        private static void RequireMessage(InvoiceDocument invoice, EdifactSegment s)
        {
            if (invoice == null) throw new InvoiceParseException("Segment " + s.Tag + " outside of a message.");
        }

        private static DateTime ParseDate(string value, string format)
        {
            var pattern = format == "203" ? "yyyyMMddHHmm" : format == "101" ? "yyMMdd" : "yyyyMMdd";
            DateTime date;
            if (value == null || !DateTime.TryParseExact(value, pattern, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                throw new InvoiceParseException("Invalid date '" + value + "'.");
            return date;
        }

        private static decimal ParseDecimal(string value, EdifactSegment s)
        {
            decimal result;
            if (value == null || !decimal.TryParse(value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out result))
                throw new InvoiceParseException("Invalid number '" + value + "' in segment " + s.Tag + ".");
            return result;
        }

        private static int ParseInt(string value, EdifactSegment s)
        {
            int result;
            if (value == null || !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
                throw new InvoiceParseException("Invalid number '" + value + "' in segment " + s.Tag + ".");
            return result;
        }
    }
}

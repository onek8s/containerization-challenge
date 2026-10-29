using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Wingtip.Invoices.Core.Parsing
{
    /// <summary>
    /// Wingtip supplier CSV layout 2.0: semicolon separated, decimal comma, dates dd.MM.yyyy.
    /// H;invoice no;invoice date;due date;VAT no;supplier name;PO number;currency;net;VAT;gross;payment reference[;type]
    /// L;line no;item no;description;quantity;unit;unit price;net;VAT %[;PO line]
    /// </summary>
    public class CsvInvoiceParser : IInvoiceParser
    {
        public SourceFormat Format
        {
            get { return SourceFormat.Csv; }
        }

        public IList<InvoiceDocument> Parse(string content)
        {
            var invoices = new List<InvoiceDocument>();
            InvoiceDocument current = null;
            var lineNo = 0;

            using (var reader = new StringReader(content))
            {
                string raw;
                while ((raw = reader.ReadLine()) != null)
                {
                    lineNo++;
                    var text = raw.TrimStart('﻿');
                    if (text.Trim().Length == 0 || text.StartsWith("#")) continue;

                    var f = Split(text);
                    switch (f[0].ToUpperInvariant())
                    {
                        case "H":
                            Require(f, 12, lineNo);
                            current = new InvoiceDocument
                            {
                                InvoiceNumber = Text(f[1]),
                                InvoiceDate = Date(f[2], lineNo),
                                DueDate = f[3].Trim().Length == 0 ? (DateTime?)null : Date(f[3], lineNo),
                                SupplierVatNumber = Text(f[4]),
                                SupplierName = Text(f[5]),
                                PurchaseOrderNumber = Text(f[6]),
                                Currency = Text(f[7]) ?? "EUR",
                                NetAmount = Number(f[8], lineNo),
                                VatAmount = Number(f[9], lineNo),
                                GrossAmount = Number(f[10], lineNo),
                                PaymentReference = Text(f[11])
                            };
                            if (f.Length > 12 && string.Equals(Text(f[12]), "CREDIT", StringComparison.OrdinalIgnoreCase))
                                current.Type = DocumentType.CreditNote;
                            invoices.Add(current);
                            break;

                        case "L":
                            if (current == null) throw new InvoiceParseException("Line " + lineNo + ": invoice line before header.");
                            Require(f, 9, lineNo);
                            var line = new InvoiceLine
                            {
                                LineNumber = (int)Number(f[1], lineNo),
                                ItemNumber = Text(f[2]),
                                Description = Text(f[3]),
                                Quantity = Number(f[4], lineNo),
                                Unit = Text(f[5]) ?? "PCE",
                                UnitPrice = Number(f[6], lineNo),
                                NetAmount = Number(f[7], lineNo),
                                VatRate = Number(f[8], lineNo),
                                PurchaseOrderNumber = current.PurchaseOrderNumber
                            };
                            if (f.Length > 9 && Text(f[9]) != null) line.PurchaseOrderLine = (int)Number(f[9], lineNo);
                            current.Lines.Add(line);
                            break;

                        default:
                            throw new InvoiceParseException("Line " + lineNo + ": unknown record type '" + f[0] + "'.");
                    }
                }
            }

            if (invoices.Count == 0) throw new InvoiceParseException("The file contains no invoices.");
            return invoices;
        }

        private static string[] Split(string line)
        {
            var fields = new List<string>();
            var sb = new System.Text.StringBuilder();
            var quoted = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else quoted = !quoted;
                }
                else if (c == ';' && !quoted)
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                }
                else sb.Append(c);
            }
            fields.Add(sb.ToString());
            return fields.ToArray();
        }

        private static void Require(string[] f, int count, int lineNo)
        {
            if (f.Length < count)
                throw new InvoiceParseException("Line " + lineNo + ": expected " + count + " fields, found " + f.Length + ".");
        }

        private static string Text(string value)
        {
            var v = value.Trim();
            return v.Length == 0 ? null : v;
        }

        private static DateTime Date(string value, int lineNo)
        {
            DateTime d;
            if (!DateTime.TryParseExact(value.Trim(), new[] { "dd.MM.yyyy", "d.M.yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                throw new InvoiceParseException("Line " + lineNo + ": invalid date '" + value + "'.");
            return d;
        }

        private static decimal Number(string value, int lineNo)
        {
            var v = value.Trim().Replace(" ", string.Empty).Replace(" ", string.Empty).Replace(',', '.');
            if (v.Length == 0) return 0m;
            decimal d;
            if (!decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out d))
                throw new InvoiceParseException("Line " + lineNo + ": invalid number '" + value + "'.");
            return d;
        }
    }
}

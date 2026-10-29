using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Wingtip.Invoices.Core.Erp
{
    /// <summary>
    /// Writes the accounts payable import file read by the ERP batch job APIMP01.
    /// Fixed-width records, CR LF line ends, amounts with two implied decimals and a trailing sign.
    /// </summary>
    public static class ErpImportFile
    {
        public const int RecordLength = 160;

        public static string FileName(long batchId, DateTime created)
        {
            return string.Format(CultureInfo.InvariantCulture, "APIMP_{0:yyyyMMddHHmmss}_{1:D6}.dat", created, batchId);
        }

        public static void Write(Stream output, Encoding encoding, long batchId, string company, DateTime created, IList<PostingInvoice> invoices)
        {
            var records = Format(batchId, company, created, invoices);
            using (var writer = new StreamWriter(output, encoding, 4096, true))
            {
                writer.NewLine = "\r\n";
                foreach (var r in records) writer.WriteLine(r);
            }
        }

        public static IList<string> Format(long batchId, string company, DateTime created, IList<PostingInvoice> invoices)
        {
            var records = new List<string>();
            records.Add(Record(
                "APH",
                Num(batchId, 10),
                Alpha(company, 4),
                created.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
                Num(invoices.Count, 6)));

            foreach (var inv in invoices)
            {
                if (Math.Abs(inv.LinesNetTotal - inv.NetAmount) > 0.005m)
                    throw new InvalidOperationException("Invoice " + inv.InvoiceId + ": lines do not add up to the net amount.");

                records.Add(Record(
                    "API",
                    Num(inv.InvoiceId, 10),
                    Alpha(inv.VendorNumber, 10),
                    Alpha(inv.InvoiceNumber, 20),
                    Num(inv.DocumentType, 3),
                    inv.InvoiceDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                    inv.DueDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                    Alpha(inv.Currency, 3),
                    Amount(inv.GrossAmount, 15),
                    Amount(inv.VatAmount, 15),
                    Alpha(inv.PaymentReference, 25),
                    Alpha(inv.PurchaseOrderNumber, 10)));

                foreach (var line in inv.Lines)
                {
                    records.Add(Record(
                        "APL",
                        Num(inv.InvoiceId, 10),
                        Num(line.LineNumber, 4),
                        Alpha(line.Account, 8),
                        Alpha(line.CostCenter, 6),
                        Amount(line.NetAmount, 15),
                        Alpha(VatCode(line.VatRate), 2),
                        Alpha(line.PurchaseOrderNumber, 10),
                        line.PurchaseOrderLine.HasValue ? Num(line.PurchaseOrderLine.Value, 4) : new string(' ', 4),
                        Quantity(line.Quantity, 12)));
                }
            }

            records.Add(Record(
                "APT",
                Num(records.Count + 1, 8),
                Amount(invoices.Sum(i => i.GrossAmount), 17)));
            return records;
        }

        public static string VatCode(decimal rate)
        {
            switch (rate.ToString("0.0", CultureInfo.InvariantCulture))
            {
                case "25.5":
                    return "S5";
                case "24.0":
                    return "S1";
                case "14.0":
                    return "R1";
                case "10.0":
                    return "R2";
                case "0.0":
                    return "Z0";
                default:
                    throw new ArgumentOutOfRangeException("rate", rate, "No ERP VAT code for the rate.");
            }
        }

        private static string Record(params string[] fields)
        {
            var r = string.Concat(fields);
            if (r.Length > RecordLength) throw new InvalidOperationException("Record longer than " + RecordLength + " characters: " + r);
            return r.PadRight(RecordLength);
        }

        private static string Alpha(string value, int length)
        {
            var v = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
            return v.Length > length ? v.Substring(0, length) : v.PadRight(length);
        }

        private static string Num(long value, int length)
        {
            var v = value.ToString(CultureInfo.InvariantCulture);
            if (v.Length > length) throw new OverflowException(value + " does not fit in " + length + " digits.");
            return v.PadLeft(length, '0');
        }

        private static string Amount(decimal value, int length)
        {
            var cents = (long)Math.Round(Math.Abs(value) * 100m, MidpointRounding.AwayFromZero);
            return Num(cents, length - 1) + (value < 0 ? "-" : "+");
        }

        private static string Quantity(decimal value, int length)
        {
            var thousandths = (long)Math.Round(Math.Abs(value) * 1000m, MidpointRounding.AwayFromZero);
            return Num(thousandths, length - 1) + (value < 0 ? "-" : "+");
        }
    }
}

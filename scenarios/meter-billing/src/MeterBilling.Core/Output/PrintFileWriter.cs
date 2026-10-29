using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Coho.Billing.Core.Output
{
    public class PrintItem
    {
        public Bill Bill { get; set; }
        public BillingAccount Account { get; set; }
    }

    /// <summary>
    /// Bill print file for the print and mail vendor (layout CWPRT3, fixed width, code page 1252, CRLF).
    /// 00 file header, 10 bill header, 11 address, 20 bill line, 30 payment, 99 trailer.
    /// </summary>
    public class PrintFileWriter
    {
        public const string Layout = "CWPRT3";
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static Encoding FileEncoding
        {
            get { return Encoding.GetEncoding(1252); }
        }

        public void Write(string runReference, DateTime createdAt, IEnumerable<PrintItem> items, Stream output)
        {
            var writer = new StreamWriter(output, FileEncoding) { NewLine = "\r\n" };
            var records = 0;
            var bills = 0;
            var total = 0m;

            writer.WriteLine("00" + Field(Layout, 6) + Field(runReference, 20) + createdAt.ToString("yyyyMMddHHmmss", Invariant));
            records++;

            foreach (var item in items)
            {
                var bill = item.Bill;
                var account = item.Account;
                bills++;
                total += bill.TotalAmount;

                writer.WriteLine("10" + Field(bill.BillNumber, 12) + Field(account.AccountNumber, 10)
                    + bill.BillDate.ToString("yyyyMMdd", Invariant) + bill.DueDate.ToString("yyyyMMdd", Invariant)
                    + bill.PeriodFrom.ToString("yyyyMMdd", Invariant) + bill.PeriodTo.AddDays(-1).ToString("yyyyMMdd", Invariant)
                    + (bill.Estimated ? "E" : "A")
                    + Number(bill.OpeningReading, 10, 1) + Number(bill.ClosingReading, 10, 1) + Number(bill.Consumption, 10, 1)
                    + Field(account.Meter != null ? account.Meter.Serial : string.Empty, 12));
                writer.WriteLine("11" + Field(account.CustomerName, 40) + Field(account.AddressLine1, 40) + Field(account.AddressLine2, 40)
                    + Field(account.PostalCode, 10) + Field(account.City, 30));
                records += 2;

                foreach (var line in bill.Lines)
                {
                    writer.WriteLine("20" + Field(line.Code, 8) + Field(line.Description, 50) + Number(line.Quantity, 11, 3)
                        + Field(line.Unit, 3) + Number(line.UnitPrice, 11, 5) + SignedNumber(line.Amount, 12, 2) + Number(line.VatRate, 5, 2));
                    records++;
                }

                writer.WriteLine("30" + SignedNumber(bill.NetAmount, 12, 2) + SignedNumber(bill.VatAmount, 12, 2) + SignedNumber(bill.TotalAmount, 12, 2)
                    + (account.PaymentMethod == PaymentMethod.DirectDebit ? "D" : "G") + Field(bill.PaymentReference, 25)
                    + Field(account.PaymentMethod == PaymentMethod.DirectDebit ? MaskIban(account.Iban) : string.Empty, 34));
                records++;
            }

            records++;
            writer.WriteLine("99" + bills.ToString("D7", Invariant) + records.ToString("D9", Invariant) + SignedNumber(total, 15, 2));
            writer.Flush();
        }

        internal static string Field(string value, int width)
        {
            value = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
            return value.Length >= width ? value.Substring(0, width) : value.PadRight(width);
        }

        internal static string Number(decimal value, int width, int decimals)
        {
            var scaled = decimal.Round(Math.Abs(value) * Pow10(decimals), 0, MidpointRounding.AwayFromZero);
            var text = scaled.ToString("0", Invariant);
            if (text.Length > width)
            {
                throw new InvalidOperationException(string.Format(Invariant, "Value {0} does not fit in {1} digits", value, width));
            }

            return text.PadLeft(width, '0');
        }

        internal static string SignedNumber(decimal value, int width, int decimals)
        {
            return (value < 0 ? "-" : "+") + Number(value, width - 1, decimals);
        }

        private static string MaskIban(string iban)
        {
            var value = Iban.Normalize(iban) ?? string.Empty;
            return value.Length <= 8 ? value : value.Substring(0, 4) + new string('*', value.Length - 8) + value.Substring(value.Length - 4);
        }

        private static decimal Pow10(int n)
        {
            var value = 1m;
            for (var i = 0; i < n; i++) value *= 10m;
            return value;
        }
    }
}

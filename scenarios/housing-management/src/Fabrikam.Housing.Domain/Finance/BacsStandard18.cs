using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Fabrikam.Housing.Finance
{
    public class BacsPayment
    {
        public string DestinationSortCode { get; set; }
        public string DestinationAccount { get; set; }
        public string DestinationName { get; set; }
        public string Reference { get; set; }
        public decimal Amount { get; set; }
    }

    /// <summary>
    /// Writes a Bacs Standard 18 credit file (single processing day, 100-character records) for the
    /// payment bureau. Header labels VOL1/HDR1/HDR2/UHL1, one 99 record per payee, a 17 contra and the
    /// EOF1/EOF2/UTL1 trailer labels.
    /// </summary>
    public class BacsStandard18
    {
        public string ServiceUserNumber { get; set; }
        public string OriginSortCode { get; set; }
        public string OriginAccount { get; set; }
        public string OriginName { get; set; } = "FABRIKAM HOMES";

        public string Build(IReadOnlyList<BacsPayment> payments, DateTime processingDate, int fileNumber)
        {
            if (payments.Count == 0) throw new InvalidOperationException("No payments to write.");
            var sb = new StringBuilder();
            var serial = fileNumber.ToString("000000");
            var julian = Julian(processingDate);
            Line(sb, "VOL1" + serial + " " + new string(' ', 31) + ServiceUserNumber.PadRight(6) + new string(' ', 28) + "1");
            Line(sb, "HDR1A" + ServiceUserNumber + "S  1" + ServiceUserNumber + serial + "00010001       " + julian + julian + " 000000" + new string(' ', 20));
            Line(sb, "HDR200000106" + new string(' ', 7) + "00" + new string(' ', 35));
            Line(sb, "UHL1" + julian + "999999    000000001 DAILY  " + fileNumber.ToString("000") + new string(' ', 40));

            long total = 0;
            foreach (var p in payments)
            {
                var pence = (long)Math.Round(p.Amount * 100m, 0, MidpointRounding.AwayFromZero);
                total += pence;
                Line(sb, Digits(p.DestinationSortCode, 6) + Digits(p.DestinationAccount, 8) + "0" + "99"
                    + Digits(OriginSortCode, 6) + Digits(OriginAccount, 8) + "    " + pence.ToString("00000000000")
                    + Text(OriginName, 18) + Text(p.Reference, 18) + Text(p.DestinationName, 18));
            }
            Line(sb, Digits(OriginSortCode, 6) + Digits(OriginAccount, 8) + "0" + "17"
                + Digits(OriginSortCode, 6) + Digits(OriginAccount, 8) + "    " + total.ToString("00000000000")
                + Text("CONTRA", 18) + Text("FABRIKAM PAYMENTS", 18) + Text(OriginName, 18));

            Line(sb, "EOF1A" + ServiceUserNumber + "S  1" + ServiceUserNumber + serial + "00010001       " + julian + julian + " 000000" + new string(' ', 20));
            Line(sb, "EOF200000106" + new string(' ', 7) + "00" + new string(' ', 35));
            Line(sb, "UTL1" + total.ToString("0000000000000") + total.ToString("0000000000000") + "0000001" + payments.Count.ToString("0000000") + new string(' ', 36));
            return sb.ToString();
        }

        /// <summary>Bacs dates are " yyddd" (space then two-digit year and day of year).</summary>
        public static string Julian(DateTime date) => (date.Year % 100).ToString("00") + date.DayOfYear.ToString("000");

        private static void Line(StringBuilder sb, string record)
        {
            if (record.Length > 100) record = record.Substring(0, 100);
            sb.Append(record.PadRight(100)).Append("\r\n");
        }

        private static string Digits(string value, int length)
        {
            var d = new string((value ?? "").Where(char.IsDigit).ToArray());
            if (d.Length != length) throw new FormatException($"Expected {length} digits but got '{value}'.");
            return d;
        }

        /// <summary>Bacs allows A-Z, 0-9, space and . / &amp; - only, upper case.</summary>
        public static string Text(string value, int length)
        {
            var upper = (value ?? "").ToUpper(CultureInfo.InvariantCulture);
            var clean = new string(upper.Select(c => char.IsLetterOrDigit(c) && c < 128 || " ./&-".IndexOf(c) >= 0 ? c : ' ').ToArray());
            return clean.Length > length ? clean.Substring(0, length) : clean.PadRight(length);
        }
    }
}

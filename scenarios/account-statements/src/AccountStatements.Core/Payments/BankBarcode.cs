using System;
using System.Globalization;
using System.Text;

namespace Woodgrove.Statements.Core.Payments
{
    /// <summary>
    /// Finnish bank barcode ("virtuaaliviivakoodi"), version 4 (national reference) and version 5 (RF reference),
    /// printed on invoices and statement payment slips.
    /// </summary>
    public static class BankBarcode
    {
        public static string Virtual(string iban, decimal amount, string reference, DateTime? dueDate)
        {
            if (string.IsNullOrWhiteSpace(iban)) throw new ArgumentException("IBAN is required.", nameof(iban));
            var account = iban.Replace(" ", "").ToUpperInvariant();
            if (!account.StartsWith("FI", StringComparison.Ordinal) || account.Length != 18) throw new ArgumentException("Finnish IBAN expected.", nameof(iban));
            if (amount < 0 || amount > 999999.99m) amount = 0m;

            var euros = (long)Math.Floor(amount);
            var cents = (int)Math.Round((amount - euros) * 100m, MidpointRounding.AwayFromZero);
            var reference_ = (reference ?? "").Replace(" ", "").ToUpperInvariant();
            var sb = new StringBuilder(54);
            if (reference_.StartsWith("RF", StringComparison.Ordinal))
            {
                var check = reference_.Substring(2, 2);
                var body = reference_.Substring(4);
                if (body.Length > 21) throw new ArgumentException("RF reference too long.", nameof(reference));
                sb.Append('5').Append(account.Substring(2));
                sb.Append(euros.ToString("000000", CultureInfo.InvariantCulture)).Append(cents.ToString("00", CultureInfo.InvariantCulture));
                sb.Append(check).Append(body.PadLeft(21, '0'));
            }
            else
            {
                if (reference_.Length > 20 || !IsDigits(reference_)) throw new ArgumentException("National reference must be up to 20 digits.", nameof(reference));
                sb.Append('4').Append(account.Substring(2));
                sb.Append(euros.ToString("000000", CultureInfo.InvariantCulture)).Append(cents.ToString("00", CultureInfo.InvariantCulture));
                sb.Append("000").Append(reference_.PadLeft(20, '0'));
            }
            sb.Append(dueDate.HasValue ? dueDate.Value.ToString("yyMMdd", CultureInfo.InvariantCulture) : "000000");
            return sb.ToString();
        }

        public static bool IsValidNationalReference(string reference)
        {
            var r = (reference ?? "").Replace(" ", "");
            if (r.Length < 4 || r.Length > 20 || !IsDigits(r)) return false;
            return CheckDigit(r.Substring(0, r.Length - 1)) == r[r.Length - 1] - '0';
        }

        public static int CheckDigit(string body)
        {
            int[] weights = { 7, 3, 1 };
            var sum = 0;
            for (int i = body.Length - 1, w = 0; i >= 0; i--, w++)
                sum += (body[i] - '0') * weights[w % 3];
            return (10 - sum % 10) % 10;
        }

        public static string FormatReference(string reference)
        {
            var r = (reference ?? "").Replace(" ", "");
            var sb = new StringBuilder();
            var first = r.Length % 5;
            if (first > 0) sb.Append(r.Substring(0, first));
            for (var i = first; i < r.Length; i += 5)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(r.Substring(i, 5));
            }
            return sb.ToString();
        }

        static bool IsDigits(string s)
        {
            foreach (var ch in s) if (ch < '0' || ch > '9') return false;
            return s.Length > 0;
        }
    }
}

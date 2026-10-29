using System.Text;

namespace Wingtip.Invoices.Core
{
    public static class VatNumber
    {
        /// <summary>Normalises VAT and business ids: FI12345678, 1234567-8 and FI 1234567-8 compare equal.</summary>
        public static string Normalize(string value)
        {
            if (value == null) return null;
            var sb = new StringBuilder();
            foreach (var c in value.ToUpperInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }
            var v = sb.ToString();
            if (v.Length == 8 && IsDigits(v)) v = "FI" + v;
            return v.Length == 0 ? null : v;
        }

        private static bool IsDigits(string v)
        {
            foreach (var c in v) if (c < '0' || c > '9') return false;
            return true;
        }
    }
}

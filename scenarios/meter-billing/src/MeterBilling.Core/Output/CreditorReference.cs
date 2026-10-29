using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Coho.Billing.Core.Output
{
    /// <summary>ISO 11649 structured creditor reference (RFxx + reference), used as the payment reference on bills.</summary>
    public static class CreditorReference
    {
        public static string Create(string reference)
        {
            if (string.IsNullOrEmpty(reference) || reference.Length > 21 || !reference.All(char.IsLetterOrDigit))
            {
                throw new ArgumentException("Reference must be 1-21 letters or digits", "reference");
            }

            var check = 98 - Mod97(reference.ToUpperInvariant() + "RF00");
            return "RF" + check.ToString("00", CultureInfo.InvariantCulture) + reference.ToUpperInvariant();
        }

        public static bool IsValid(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            var compact = value.Replace(" ", string.Empty).ToUpperInvariant();
            if (compact.Length < 5 || compact.Length > 25 || !compact.StartsWith("RF", StringComparison.Ordinal) || !compact.All(char.IsLetterOrDigit))
            {
                return false;
            }

            return Mod97(compact.Substring(4) + compact.Substring(0, 4)) == 1;
        }

        public static string Format(string value)
        {
            var compact = value.Replace(" ", string.Empty);
            var sb = new StringBuilder();
            for (var i = 0; i < compact.Length; i++)
            {
                if (i > 0 && i % 4 == 0) sb.Append(' ');
                sb.Append(compact[i]);
            }

            return sb.ToString();
        }

        internal static int Mod97(string value)
        {
            var remainder = 0;
            foreach (var c in value)
            {
                var digits = char.IsDigit(c) ? (c - '0').ToString(CultureInfo.InvariantCulture) : (c - 'A' + 10).ToString(CultureInfo.InvariantCulture);
                foreach (var d in digits)
                {
                    remainder = (remainder * 10 + (d - '0')) % 97;
                }
            }

            return remainder;
        }
    }

    public static class Iban
    {
        public static string Normalize(string iban)
        {
            return iban == null ? null : iban.Replace(" ", string.Empty).ToUpperInvariant();
        }

        public static bool IsValid(string iban)
        {
            var value = Normalize(iban);
            if (string.IsNullOrEmpty(value) || value.Length < 15 || value.Length > 34)
            {
                return false;
            }

            if (!char.IsLetter(value[0]) || !char.IsLetter(value[1]) || !char.IsDigit(value[2]) || !char.IsDigit(value[3]))
            {
                return false;
            }

            if (!value.All(c => (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))
            {
                return false;
            }

            return CreditorReference.Mod97(value.Substring(4) + value.Substring(0, 4)) == 1;
        }
    }
}

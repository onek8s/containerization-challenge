using System;
using System.Linq;

namespace VanArsdel.Integration.Domain.Codes
{
    /// <summary>GS1 mod-10 check digits for GTIN-8/12/13/14, GLN (13) and SSCC (18).</summary>
    public static class CheckDigit
    {
        public static int Calculate(string digitsWithoutCheck)
        {
            if (string.IsNullOrEmpty(digitsWithoutCheck) || !digitsWithoutCheck.All(char.IsDigit))
            {
                throw new ArgumentException("Only digits are allowed.", "digitsWithoutCheck");
            }

            var sum = 0;
            var weight = 3;
            for (var i = digitsWithoutCheck.Length - 1; i >= 0; i--)
            {
                sum += (digitsWithoutCheck[i] - '0') * weight;
                weight = weight == 3 ? 1 : 3;
            }

            return (10 - (sum % 10)) % 10;
        }

        public static bool IsValid(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length < 8 || !code.All(char.IsDigit))
            {
                return false;
            }

            return Calculate(code.Substring(0, code.Length - 1)) == code[code.Length - 1] - '0';
        }

        public static bool IsValidGtin(string gtin)
        {
            if (gtin == null)
            {
                return false;
            }

            var len = gtin.Length;
            return (len == 8 || len == 12 || len == 13 || len == 14) && IsValid(gtin);
        }

        public static bool IsValidGln(string gln)
        {
            return gln != null && gln.Length == 13 && IsValid(gln);
        }

        /// <summary>GTIN-12 (UPC-A, as US partners send it in PO107) or GTIN-13 widened to GTIN-14.</summary>
        public static string ToGtin14(string gtin)
        {
            if (!IsValidGtin(gtin))
            {
                return null;
            }

            return gtin.PadLeft(14, '0');
        }

        public static string Append(string digitsWithoutCheck)
        {
            return digitsWithoutCheck + Calculate(digitsWithoutCheck).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}

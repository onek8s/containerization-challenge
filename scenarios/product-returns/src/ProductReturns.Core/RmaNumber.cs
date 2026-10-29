using System;
using System.Globalization;

namespace ProductReturns.Core
{
    /// <summary>
    /// Return authorisation numbers look like R2410-004711-3: R, year and month of creation,
    /// the request id and a Luhn check digit. The warehouse scanners verify the check digit.
    /// </summary>
    public static class RmaNumber
    {
        public static string Create(int id, DateTime created)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

            var body = created.ToString("yyMM", CultureInfo.InvariantCulture) + id.ToString("D6", CultureInfo.InvariantCulture);
            return "R" + body.Substring(0, 4) + "-" + body.Substring(4) + "-" + CheckDigit(body);
        }

        public static bool IsValid(string rma)
        {
            if (!TryNormalize(rma, out var normalized))
            {
                return false;
            }

            var body = normalized.Substring(1, 4) + normalized.Substring(6, normalized.Length - 8);
            return normalized[normalized.Length - 1] == CheckDigit(body);
        }

        /// <summary>Accepts scanner input without dashes or with lower case letters.</summary>
        public static bool TryNormalize(string input, out string rma)
        {
            rma = null;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var compact = input.Trim().Replace("-", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
            if (compact.Length < 12 || compact[0] != 'R')
            {
                return false;
            }

            for (var i = 1; i < compact.Length; i++)
            {
                if (!char.IsDigit(compact[i]))
                {
                    return false;
                }
            }

            rma = "R" + compact.Substring(1, 4) + "-" + compact.Substring(5, compact.Length - 6) + "-" + compact[compact.Length - 1];
            return true;
        }

        public static int IdOf(string rma)
        {
            if (!TryNormalize(rma, out var normalized))
            {
                return 0;
            }

            var idPart = normalized.Substring(6, normalized.Length - 8);
            return int.TryParse(idPart, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : 0;
        }

        private static char CheckDigit(string digits)
        {
            var sum = 0;
            var doubleIt = true;
            for (var i = digits.Length - 1; i >= 0; i--)
            {
                var d = digits[i] - '0';
                if (doubleIt)
                {
                    d *= 2;
                    if (d > 9) d -= 9;
                }

                sum += d;
                doubleIt = !doubleIt;
            }

            return (char)('0' + (10 - sum % 10) % 10);
        }
    }
}

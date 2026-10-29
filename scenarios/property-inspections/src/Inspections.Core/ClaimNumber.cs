using System;
using System.Text.RegularExpressions;

namespace Northwind.Inspections.Core
{
    /// <summary>
    /// Claim numbers are issued by the claims system as LOB-YYYY-NNNNNN, for example HO-2024-004512.
    /// Adjusters often type them without dashes or in lower case.
    /// </summary>
    public static class ClaimNumber
    {
        private static readonly Regex Pattern = new Regex(@"^(HO|CO|LL|FL)-?(\d{4})-?(\d{6})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static readonly string[] LinesOfBusiness = { "HO", "CO", "LL", "FL" };

        public static bool TryNormalize(string value, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var match = Pattern.Match(value.Trim().Replace(" ", string.Empty));
            if (!match.Success)
            {
                return false;
            }

            var year = int.Parse(match.Groups[2].Value);
            if (year < 2000 || year > 2099)
            {
                return false;
            }

            normalized = string.Format("{0}-{1}-{2}", match.Groups[1].Value.ToUpperInvariant(), match.Groups[2].Value, match.Groups[3].Value);
            return true;
        }

        public static string Normalize(string value)
        {
            string normalized;
            if (!TryNormalize(value, out normalized))
            {
                throw new FormatException("'" + value + "' is not a valid claim number.");
            }
            return normalized;
        }

        public static int Year(string claimNumber)
        {
            return int.Parse(Normalize(claimNumber).Substring(3, 4));
        }
    }
}

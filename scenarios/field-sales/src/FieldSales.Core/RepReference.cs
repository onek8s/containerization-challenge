using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Wingtip.FieldSales.Core
{
    /// <summary>Order numbers the client gives while offline: rep code, order date and a running number per day.</summary>
    public static class RepReference
    {
        private static readonly Regex Pattern = new Regex(@"^[A-Z]{2,4}-\d{6}-\d{3}$");

        public static string Create(string repCode, DateTime date, int sequence)
        {
            if (string.IsNullOrWhiteSpace(repCode)) throw new ArgumentException("Rep code is required.", nameof(repCode));
            if (sequence < 1 || sequence > 999) throw new ArgumentOutOfRangeException(nameof(sequence));
            return string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyMMdd}-{2:000}", repCode.ToUpperInvariant(), date, sequence);
        }

        public static bool IsValid(string value)
        {
            return value != null && Pattern.IsMatch(value);
        }
    }
}

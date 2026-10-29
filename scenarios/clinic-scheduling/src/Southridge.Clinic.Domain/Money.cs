using System;
using System.Globalization;

namespace Southridge.Clinic.Domain
{
    public static class Money
    {
        public static decimal Round(decimal amount)
        {
            return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        }

        public static string Format(decimal amount)
        {
            return amount.ToString("C", CultureInfo.GetCultureInfo("en-US"));
        }

        /// <summary>X12 amounts: no thousands separator, no trailing zeros after the point.</summary>
        public static string ToX12(decimal amount)
        {
            string s = Round(amount).ToString("0.00", CultureInfo.InvariantCulture);
            if (s.EndsWith(".00"))
            {
                return s.Substring(0, s.Length - 3);
            }

            return s.EndsWith("0") ? s.Substring(0, s.Length - 1) : s;
        }
    }
}

using System;
using System.Globalization;

namespace Northwind.Inspections.Core
{
    public static class Watermark
    {
        public static string Text(string claimNumber, DateTime takenOrUploaded, int sequence, string adjusterName)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}  #{1:00}  {2:yyyy-MM-dd HH:mm}  {3}",
                ClaimNumber.Normalize(claimNumber), sequence, takenOrUploaded, Shorten(adjusterName, 32));
        }

        public static string Caption(string caption, int maxLength)
        {
            return Shorten((caption ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim(), maxLength);
        }

        private static string Shorten(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max)
            {
                return value ?? string.Empty;
            }
            return value.Substring(0, max - 1) + "…";
        }
    }
}

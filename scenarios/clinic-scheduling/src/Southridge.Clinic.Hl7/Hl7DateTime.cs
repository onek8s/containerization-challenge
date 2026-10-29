using System;
using System.Globalization;

namespace Southridge.Clinic.Hl7
{
    public static class Hl7DateTime
    {
        private static readonly string[] Formats = new[]
        {
            "yyyyMMddHHmmss", "yyyyMMddHHmm", "yyyyMMddHH", "yyyyMMdd", "yyyyMMddHHmmss.f", "yyyyMMddHHmmss.ff", "yyyyMMddHHmmss.fff", "yyyyMMddHHmmss.ffff"
        };

        public static string Format(DateTime value)
        {
            return value.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        }

        public static string FormatDate(DateTime value)
        {
            return value.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        public static string Format(DateTime? value)
        {
            return value.HasValue ? Format(value.Value) : string.Empty;
        }

        public static DateTime? Parse(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            value = value.Trim();
            int zone = value.IndexOfAny(new[] { '+', '-' });
            if (zone > 8)
            {
                // Senders include their UTC offset; times are kept as clinic local time like the rest of the system.
                value = value.Substring(0, zone);
            }

            DateTime result;
            if (DateTime.TryParseExact(value, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            {
                return result;
            }

            return null;
        }
    }
}

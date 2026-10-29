using System;
using System.Globalization;

namespace VanArsdel.Integration.Domain.Codes
{
    /// <summary>Date and time formats used in X12 (DT 6/8, TM 4/6/8) and EDIFACT DTM format qualifiers.</summary>
    public static class EdiDate
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static DateTime? ParseX12Date(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            value = value.Trim();
            DateTime result;
            if (value.Length == 8 && DateTime.TryParseExact(value, "yyyyMMdd", Inv, DateTimeStyles.None, out result))
            {
                return result;
            }

            if (value.Length == 6 && DateTime.TryParseExact(value, "yyMMdd", Inv, DateTimeStyles.None, out result))
            {
                return result;
            }

            return null;
        }

        public static TimeSpan? ParseX12Time(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            value = value.Trim();
            int h, m, s = 0;
            if (value.Length < 4 || !int.TryParse(value.Substring(0, 2), NumberStyles.None, Inv, out h) || !int.TryParse(value.Substring(2, 2), NumberStyles.None, Inv, out m))
            {
                return null;
            }

            if (value.Length >= 6 && !int.TryParse(value.Substring(4, 2), NumberStyles.None, Inv, out s))
            {
                return null;
            }

            if (h > 23 || m > 59 || s > 59)
            {
                return null;
            }

            return new TimeSpan(h, m, s);
        }

        /// <summary>EDIFACT DTM C507: 102 = CCYYMMDD, 203 = CCYYMMDDHHMM, 101 = YYMMDD, 204 = CCYYMMDDHHMMSS, 718 = range (first date used).</summary>
        public static DateTime? ParseEdifact(string value, string formatQualifier)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            value = value.Trim();
            string pattern;
            switch ((formatQualifier ?? "102").Trim())
            {
                case "101":
                    pattern = "yyMMdd";
                    break;
                case "102":
                    pattern = "yyyyMMdd";
                    break;
                case "203":
                    pattern = "yyyyMMddHHmm";
                    break;
                case "204":
                    pattern = "yyyyMMddHHmmss";
                    break;
                case "718":
                    pattern = "yyyyMMdd";
                    value = value.Length >= 8 ? value.Substring(0, 8) : value;
                    break;
                default:
                    return null;
            }

            DateTime result;
            return DateTime.TryParseExact(value, pattern, Inv, DateTimeStyles.None, out result) ? result : (DateTime?)null;
        }

        public static string ToX12Date(DateTime value)
        {
            return value.ToString("yyyyMMdd", Inv);
        }

        public static string ToX12Time(DateTime value)
        {
            return value.ToString("HHmm", Inv);
        }

        public static string ToEdifact(DateTime value, string formatQualifier)
        {
            switch (formatQualifier)
            {
                case "101":
                    return value.ToString("yyMMdd", Inv);
                case "203":
                    return value.ToString("yyyyMMddHHmm", Inv);
                case "204":
                    return value.ToString("yyyyMMddHHmmss", Inv);
                default:
                    return value.ToString("yyyyMMdd", Inv);
            }
        }

        public static string ToXmlDate(DateTime value)
        {
            return value.ToString("yyyy-MM-dd", Inv);
        }
    }
}

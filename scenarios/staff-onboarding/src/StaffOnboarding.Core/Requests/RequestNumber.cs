using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace StaffOnboarding.Core.Requests
{
    /// <summary>Request numbers look like ONB-2026-00042, MOV-2026-00007 or LVR-2026-00113.</summary>
    public static class RequestNumber
    {
        private static readonly Regex Pattern = new Regex(@"^(ONB|MOV|LVR)-(\d{4})-(\d{5})$", RegexOptions.CultureInvariant);

        public static string Format(RequestKind kind, int year, int sequence)
        {
            if (sequence < 1 || sequence > 99999) throw new ArgumentOutOfRangeException("sequence");
            return Prefix(kind) + "-" + year.ToString("0000", CultureInfo.InvariantCulture) + "-" +
                   sequence.ToString("00000", CultureInfo.InvariantCulture);
        }

        public static bool TryParse(string value, out RequestKind kind, out int year, out int sequence)
        {
            kind = 0;
            year = 0;
            sequence = 0;
            if (value == null) return false;
            var m = Pattern.Match(value.Trim().ToUpperInvariant());
            if (!m.Success) return false;
            switch (m.Groups[1].Value)
            {
                case "ONB": kind = RequestKind.Joiner; break;
                case "MOV": kind = RequestKind.Mover; break;
                default: kind = RequestKind.Leaver; break;
            }
            year = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            sequence = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            return sequence > 0;
        }

        public static string Prefix(RequestKind kind)
        {
            switch (kind)
            {
                case RequestKind.Joiner: return "ONB";
                case RequestKind.Mover: return "MOV";
                case RequestKind.Leaver: return "LVR";
                default: throw new ArgumentOutOfRangeException("kind");
            }
        }
    }
}

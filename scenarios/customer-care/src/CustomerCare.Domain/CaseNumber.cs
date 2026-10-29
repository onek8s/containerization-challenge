using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Tailspin.CustomerCare.Domain
{
    /// <summary>Case numbers look like CC-2026-004512 (year of creation and a per-year sequence).</summary>
    public static class CaseNumber
    {
        private static readonly Regex Pattern = new Regex(@"^CC-(\d{4})-(\d{6})$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static string Format(int year, int sequence)
        {
            if (year < 2000 || year > 2099) throw new ArgumentOutOfRangeException(nameof(year));
            if (sequence < 1 || sequence > 999999) throw new ArgumentOutOfRangeException(nameof(sequence));
            return string.Format(CultureInfo.InvariantCulture, "CC-{0:0000}-{1:000000}", year, sequence);
        }

        public static bool TryParse(string value, out int year, out int sequence)
        {
            year = 0;
            sequence = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;

            var m = Pattern.Match(value.Trim().ToUpperInvariant());
            if (!m.Success) return false;

            year = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            sequence = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            return sequence > 0;
        }

        /// <summary>Agents type "4512" or "cc-2026-4512" into the search box; normalise what we can.</summary>
        public static string Normalize(string value, int currentYear)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var v = value.Trim().ToUpperInvariant();

            int year, seq;
            if (TryParse(v, out year, out seq)) return Format(year, seq);

            var loose = Regex.Match(v, @"^(?:CC-?)?(?:(\d{4})-)?(\d{1,6})$");
            if (!loose.Success) return null;

            year = loose.Groups[1].Success ? int.Parse(loose.Groups[1].Value, CultureInfo.InvariantCulture) : currentYear;
            seq = int.Parse(loose.Groups[2].Value, CultureInfo.InvariantCulture);
            if (seq == 0 || year < 2000 || year > 2099) return null;
            return Format(year, seq);
        }
    }
}

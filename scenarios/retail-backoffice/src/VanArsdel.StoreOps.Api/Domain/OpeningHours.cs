using System;
using System.Globalization;

namespace VanArsdel.StoreOps.Api.Domain
{
    /// <summary>
    /// Opening hours as maintained by the store managers: "Mon-Fri 10-20; Sat 10-18; Sun 12-16".
    /// Missing days mean closed.
    /// </summary>
    public static class OpeningHours
    {
        private static readonly string[] Days = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

        public static bool IsOpen(string spec, DateTime localTime)
        {
            if (string.IsNullOrWhiteSpace(spec)) return false;
            var day = (int)localTime.DayOfWeek;
            foreach (var part in spec.Split(';'))
            {
                var tokens = part.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length != 2) continue;
                if (!CoversDay(tokens[0], day)) continue;
                var hours = tokens[1].Split('-');
                if (hours.Length != 2) continue;
                var from = ParseTime(hours[0]);
                var to = ParseTime(hours[1]);
                var now = localTime.TimeOfDay;
                return now >= from && now < to;
            }
            return false;
        }

        private static bool CoversDay(string range, int day)
        {
            var ends = range.Split('-');
            var start = Array.FindIndex(Days, d => d.Equals(ends[0], StringComparison.OrdinalIgnoreCase));
            var end = ends.Length > 1 ? Array.FindIndex(Days, d => d.Equals(ends[1], StringComparison.OrdinalIgnoreCase)) : start;
            if (start < 0 || end < 0) return false;
            // Mon-Sun wraps over the week start.
            return start <= end ? day >= start && day <= end : day >= start || day <= end;
        }

        private static TimeSpan ParseTime(string value)
        {
            var parts = value.Split(':', '.');
            var h = int.Parse(parts[0], CultureInfo.InvariantCulture);
            var m = parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 0;
            return new TimeSpan(h, m, 0);
        }
    }
}

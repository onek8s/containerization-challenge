using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace StaffOnboarding.Core.Scheduling
{
    /// <summary>Working days: Monday to Friday excluding company holidays.</summary>
    public class BusinessCalendar
    {
        private readonly HashSet<DateTime> _holidays;

        public BusinessCalendar(IEnumerable<DateTime> holidays)
        {
            _holidays = new HashSet<DateTime>((holidays ?? Enumerable.Empty<DateTime>()).Select(d => d.Date));
        }

        public IEnumerable<DateTime> Holidays
        {
            get { return _holidays.OrderBy(d => d); }
        }

        public bool IsBusinessDay(DateTime date)
        {
            var d = date.Date;
            return d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday && !_holidays.Contains(d);
        }

        /// <summary>Moves <paramref name="days"/> business days back (negative) or forward (positive).</summary>
        public DateTime AddBusinessDays(DateTime date, int days)
        {
            var d = date.Date;
            var step = days < 0 ? -1 : 1;
            var remaining = Math.Abs(days);
            while (remaining > 0)
            {
                d = d.AddDays(step);
                if (IsBusinessDay(d)) remaining--;
            }
            return d;
        }

        public DateTime NextBusinessDay(DateTime date)
        {
            var d = date.Date;
            while (!IsBusinessDay(d)) d = d.AddDays(1);
            return d;
        }

        /// <summary>Reads one date per line (yyyy-MM-dd); text after '#' is ignored.</summary>
        public static BusinessCalendar Parse(TextReader reader)
        {
            var dates = new List<DateTime>();
            string line;
            var lineNumber = 0;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                var hash = line.IndexOf('#');
                if (hash >= 0) line = line.Substring(0, hash);
                line = line.Trim();
                if (line.Length == 0) continue;
                DateTime d;
                if (!DateTime.TryParseExact(line, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                    throw new FormatException("Invalid holiday date on line " + lineNumber + ": " + line);
                dates.Add(d);
            }
            return new BusinessCalendar(dates);
        }

        public static BusinessCalendar Load(string path)
        {
            using (var reader = new StreamReader(path))
            {
                return Parse(reader);
            }
        }
    }
}

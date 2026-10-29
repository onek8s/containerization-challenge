using System;
using System.Collections.Generic;

namespace Southridge.Clinic.Domain
{
    /// <summary>Clinic holidays (all sites closed) and business day arithmetic.</summary>
    public class ClinicCalendar
    {
        private readonly HashSet<DateTime> _extraClosedDays;

        public ClinicCalendar()
            : this(new DateTime[0])
        {
        }

        public ClinicCalendar(IEnumerable<DateTime> extraClosedDays)
        {
            _extraClosedDays = new HashSet<DateTime>();
            foreach (DateTime d in extraClosedDays)
            {
                _extraClosedDays.Add(d.Date);
            }
        }

        public bool IsClosed(DateTime date)
        {
            date = date.Date;
            return date.DayOfWeek == DayOfWeek.Sunday || IsHoliday(date) || _extraClosedDays.Contains(date);
        }

        public bool IsHoliday(DateTime date)
        {
            date = date.Date;
            int y = date.Year;
            return date == Observed(new DateTime(y, 1, 1))
                || date == NthWeekday(y, 5, DayOfWeek.Monday, -1)
                || date == Observed(new DateTime(y, 7, 4))
                || date == NthWeekday(y, 9, DayOfWeek.Monday, 1)
                || date == NthWeekday(y, 11, DayOfWeek.Thursday, 4)
                || date == Observed(new DateTime(y, 12, 25));
        }

        public DateTime AddBusinessDays(DateTime start, int days)
        {
            DateTime d = start.Date;
            int step = days < 0 ? -1 : 1;
            int remaining = Math.Abs(days);
            while (remaining > 0)
            {
                d = d.AddDays(step);
                if (d.DayOfWeek != DayOfWeek.Saturday && !IsClosed(d))
                {
                    remaining--;
                }
            }

            return d;
        }

        public DateTime NextOpenDay(DateTime date)
        {
            DateTime d = date.Date.AddDays(1);
            while (IsClosed(d))
            {
                d = d.AddDays(1);
            }

            return d;
        }

        private static DateTime Observed(DateTime holiday)
        {
            if (holiday.DayOfWeek == DayOfWeek.Saturday)
            {
                return holiday.AddDays(-1);
            }

            return holiday.DayOfWeek == DayOfWeek.Sunday ? holiday.AddDays(1) : holiday;
        }

        private static DateTime NthWeekday(int year, int month, DayOfWeek day, int n)
        {
            if (n > 0)
            {
                DateTime d = new DateTime(year, month, 1);
                while (d.DayOfWeek != day)
                {
                    d = d.AddDays(1);
                }

                return d.AddDays(7 * (n - 1));
            }

            DateTime last = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            while (last.DayOfWeek != day)
            {
                last = last.AddDays(-1);
            }

            return last;
        }
    }
}

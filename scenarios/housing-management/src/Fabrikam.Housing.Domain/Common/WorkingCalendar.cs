using System;
using System.Collections.Generic;
using System.Linq;

namespace Fabrikam.Housing.Common
{
    /// <summary>
    /// Working days for England and Wales. Bank holidays are kept in the BankHolidays table and
    /// loaded at start-up; the defaults below cover 2019-2022 for when the table is empty.
    /// </summary>
    public class WorkingCalendar
    {
        public static readonly DateTime[] DefaultBankHolidays =
        {
            new DateTime(2019, 1, 1), new DateTime(2019, 4, 19), new DateTime(2019, 4, 22), new DateTime(2019, 5, 6),
            new DateTime(2019, 5, 27), new DateTime(2019, 8, 26), new DateTime(2019, 12, 25), new DateTime(2019, 12, 26),
            new DateTime(2020, 1, 1), new DateTime(2020, 4, 10), new DateTime(2020, 4, 13), new DateTime(2020, 5, 8),
            new DateTime(2020, 5, 25), new DateTime(2020, 8, 31), new DateTime(2020, 12, 25), new DateTime(2020, 12, 28),
            new DateTime(2021, 1, 1), new DateTime(2021, 4, 2), new DateTime(2021, 4, 5), new DateTime(2021, 5, 3),
            new DateTime(2021, 5, 31), new DateTime(2021, 8, 30), new DateTime(2021, 12, 27), new DateTime(2021, 12, 28),
            new DateTime(2022, 1, 3), new DateTime(2022, 4, 15), new DateTime(2022, 4, 18), new DateTime(2022, 5, 2),
            new DateTime(2022, 6, 2), new DateTime(2022, 6, 3), new DateTime(2022, 8, 29), new DateTime(2022, 9, 19),
            new DateTime(2022, 12, 26), new DateTime(2022, 12, 27)
        };

        private readonly HashSet<DateTime> _holidays;

        public WorkingCalendar() : this(DefaultBankHolidays) { }

        public WorkingCalendar(IEnumerable<DateTime> bankHolidays)
        {
            _holidays = new HashSet<DateTime>(bankHolidays.Select(d => d.Date));
        }

        public bool IsWorkingDay(DateTime date)
        {
            return date.DayOfWeek != DayOfWeek.Saturday
                && date.DayOfWeek != DayOfWeek.Sunday
                && !_holidays.Contains(date.Date);
        }

        public DateTime AddWorkingDays(DateTime start, int days)
        {
            var date = start.Date;
            var added = 0;
            while (added < days)
            {
                date = date.AddDays(1);
                if (IsWorkingDay(date)) added++;
            }
            return date.Add(start.TimeOfDay);
        }

        public DateTime NextWorkingDay(DateTime date)
        {
            var d = date.Date;
            while (!IsWorkingDay(d)) d = d.AddDays(1);
            return d;
        }

        public int WorkingDaysBetween(DateTime from, DateTime to)
        {
            var count = 0;
            for (var d = from.Date.AddDays(1); d <= to.Date; d = d.AddDays(1))
            {
                if (IsWorkingDay(d)) count++;
            }
            return count;
        }
    }
}

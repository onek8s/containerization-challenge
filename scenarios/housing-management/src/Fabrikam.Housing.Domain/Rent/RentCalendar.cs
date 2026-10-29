using System;
using System.Collections.Generic;
using System.Linq;

namespace Fabrikam.Housing.Rent
{
    /// <summary>
    /// Fabrikam charges rent over 48 weeks of the 52/53-week rent year (which starts on the first Monday in April).
    /// The four rent-free weeks are the two weeks covering Christmas and New Year and the last two weeks
    /// of the rent year in March.
    /// </summary>
    public class RentCalendar
    {
        public static DateTime WeekStart(DateTime date)
        {
            var d = date.Date;
            var diff = ((int)d.DayOfWeek + 6) % 7;
            return d.AddDays(-diff);
        }

        public static DateTime RentYearStart(int year)
        {
            var d = new DateTime(year, 4, 1);
            while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(1);
            return d;
        }

        public static DateTime RentYearStartFor(DateTime date)
        {
            var start = RentYearStart(date.Year);
            return date.Date >= start ? start : RentYearStart(date.Year - 1);
        }

        public static int WeekNumber(DateTime date)
        {
            var start = RentYearStartFor(date);
            return (int)((WeekStart(date) - start).TotalDays / 7) + 1;
        }

        public static int WeeksInRentYear(DateTime rentYearStart)
        {
            return (int)((RentYearStart(rentYearStart.Year + 1) - rentYearStart).TotalDays / 7);
        }

        public IReadOnlyList<DateTime> RentFreeWeeks(DateTime rentYearStart)
        {
            var weeks = new List<DateTime>();
            var christmas = WeekStart(new DateTime(rentYearStart.Year, 12, 25));
            weeks.Add(christmas);
            weeks.Add(christmas.AddDays(7));
            var nextStart = RentYearStart(rentYearStart.Year + 1);
            weeks.Add(nextStart.AddDays(-14));
            weeks.Add(nextStart.AddDays(-7));
            return weeks;
        }

        public bool IsRentFreeWeek(DateTime date)
        {
            var week = WeekStart(date);
            return RentFreeWeeks(RentYearStartFor(week)).Contains(week);
        }

        /// <summary>
        /// Converts the 52-week equivalent weekly rent used in rent setting into the amount charged
        /// in each of the 48 charging weeks.
        /// </summary>
        public static decimal ChargingWeekAmount(decimal weekly52, int weeksInYear = 52)
        {
            return Math.Round(weekly52 * weeksInYear / (weeksInYear - 4), 2, MidpointRounding.AwayFromZero);
        }

        public IEnumerable<DateTime> ChargingWeeksBetween(DateTime from, DateTime to)
        {
            for (var w = WeekStart(from); w <= to.Date; w = w.AddDays(7))
            {
                if (!IsRentFreeWeek(w)) yield return w;
            }
        }
    }
}

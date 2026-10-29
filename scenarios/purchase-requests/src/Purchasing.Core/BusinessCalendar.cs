using System;

namespace Contoso.Purchasing.Core
{
    public static class BusinessCalendar
    {
        public static int BusinessDaysBetween(DateTime from, DateTime to)
        {
            if (to <= from) return 0;

            var days = 0;
            var day = from.Date.AddDays(1);
            while (day <= to.Date)
            {
                if (day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday)
                {
                    days++;
                }
                day = day.AddDays(1);
            }
            return days;
        }
    }
}

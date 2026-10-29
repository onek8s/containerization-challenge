using System;
using System.Collections.Generic;
using System.Linq;

namespace VanArsdel.Integration.Rules
{
    /// <summary>Order desk calendar: same-day cut-off and holidays.</summary>
    [Serializable]
    public class CalendarFacts
    {
        private readonly HashSet<DateTime> _holidays;

        public CalendarFacts(DateTime nowLocal, int cutOffHour, IEnumerable<DateTime> holidays)
        {
            Now = nowLocal;
            CutOffHour = cutOffHour;
            _holidays = new HashSet<DateTime>((holidays ?? Enumerable.Empty<DateTime>()).Select(d => d.Date));
        }

        public DateTime Now { get; private set; }
        public int CutOffHour { get; private set; }

        public DateTime Today
        {
            get { return Now.Date; }
        }

        public bool IsAfterCutOff()
        {
            return Now.Hour >= CutOffHour;
        }

        public bool IsWeekendOrHoliday()
        {
            return Now.DayOfWeek == DayOfWeek.Saturday || Now.DayOfWeek == DayOfWeek.Sunday || _holidays.Contains(Now.Date);
        }

        public int DayOfWeekNumber()
        {
            return (int)Now.DayOfWeek;
        }
    }
}

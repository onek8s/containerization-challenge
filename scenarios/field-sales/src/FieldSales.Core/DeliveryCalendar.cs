using System;
using System.Collections.Generic;
using System.Linq;

namespace Wingtip.FieldSales.Core
{
    /// <summary>
    /// Earliest possible delivery: orders received before the cut-off are picked the next working day and
    /// delivered on the customer's route day after that. Later orders lose one more working day.
    /// </summary>
    public class DeliveryCalendar
    {
        private readonly HashSet<DateTime> _holidays;

        public DeliveryCalendar(TimeSpan cutOff, IEnumerable<DateTime> holidays)
        {
            CutOff = cutOff;
            _holidays = new HashSet<DateTime>((holidays ?? Enumerable.Empty<DateTime>()).Select(h => h.Date));
        }

        public TimeSpan CutOff { get; private set; }

        public bool IsWorkingDay(DateTime day)
        {
            return day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday && !_holidays.Contains(day.Date);
        }

        public DateTime EarliestDelivery(DateTime orderTime)
        {
            var days = orderTime.TimeOfDay < CutOff ? 1 : 2;
            var day = orderTime.Date;
            while (days > 0)
            {
                day = day.AddDays(1);
                if (IsWorkingDay(day)) days--;
            }
            return day;
        }

        public DateTime NextRouteDay(DateTime orderTime, DeliveryDays route)
        {
            if (route == DeliveryDays.None) throw new ArgumentException("Customer has no delivery days.", nameof(route));

            var day = EarliestDelivery(orderTime);
            while (!route.Includes(day.DayOfWeek) || !IsWorkingDay(day)) day = day.AddDays(1);
            return day;
        }

        public bool IsValidDelivery(DateTime orderTime, DeliveryDays route, DateTime requested)
        {
            return requested.Date >= EarliestDelivery(orderTime)
                && route.Includes(requested.DayOfWeek)
                && IsWorkingDay(requested);
        }
    }
}

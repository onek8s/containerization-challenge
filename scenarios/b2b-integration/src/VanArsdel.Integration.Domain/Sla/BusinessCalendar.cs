using System;
using System.Collections.Generic;
using System.Linq;

namespace VanArsdel.Integration.Domain.Sla
{
    /// <summary>
    /// Customer service hours of the order desk (Mon-Fri, 07:00-18:00, company time). SLA rules flagged
    /// BusinessHoursOnly only count time inside these hours. Holidays come from dbo.Holiday.
    /// </summary>
    public class BusinessCalendar
    {
        private readonly HashSet<DateTime> _holidays;

        public BusinessCalendar(TimeSpan open, TimeSpan close, IEnumerable<DateTime> holidays, int utcOffsetMinutes)
        {
            if (close <= open)
            {
                throw new ArgumentException("Close must be after open.");
            }

            Open = open;
            Close = close;
            UtcOffset = TimeSpan.FromMinutes(utcOffsetMinutes);
            _holidays = new HashSet<DateTime>((holidays ?? Enumerable.Empty<DateTime>()).Select(d => d.Date));
        }

        public TimeSpan Open { get; private set; }
        public TimeSpan Close { get; private set; }
        public TimeSpan UtcOffset { get; private set; }

        public static BusinessCalendar Default
        {
            get { return new BusinessCalendar(TimeSpan.FromHours(7), TimeSpan.FromHours(18), null, 0); }
        }

        public bool IsWorkingDay(DateTime localDate)
        {
            return localDate.DayOfWeek != DayOfWeek.Saturday
                   && localDate.DayOfWeek != DayOfWeek.Sunday
                   && !_holidays.Contains(localDate.Date);
        }

        /// <summary>Business time between two UTC instants.</summary>
        public TimeSpan BusinessTimeBetween(DateTime fromUtc, DateTime toUtc)
        {
            if (toUtc <= fromUtc)
            {
                return TimeSpan.Zero;
            }

            var from = fromUtc + UtcOffset;
            var to = toUtc + UtcOffset;
            var total = TimeSpan.Zero;
            for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
            {
                if (!IsWorkingDay(day))
                {
                    continue;
                }

                var start = day + Open;
                var end = day + Close;
                var a = from > start ? from : start;
                var b = to < end ? to : end;
                if (b > a)
                {
                    total += b - a;
                }
            }

            return total;
        }

        /// <summary>The UTC instant when the given amount of business time has passed after fromUtc.</summary>
        public DateTime AddBusinessTime(DateTime fromUtc, TimeSpan amount)
        {
            var local = fromUtc + UtcOffset;
            var remaining = amount;
            var cursor = local;
            for (var guard = 0; guard < 400; guard++)
            {
                var day = cursor.Date;
                if (IsWorkingDay(day))
                {
                    var start = day + Open;
                    var end = day + Close;
                    if (cursor < start)
                    {
                        cursor = start;
                    }

                    if (cursor < end)
                    {
                        var available = end - cursor;
                        if (available >= remaining)
                        {
                            return cursor + remaining - UtcOffset;
                        }

                        remaining -= available;
                    }
                }

                cursor = day.AddDays(1) + Open;
            }

            throw new InvalidOperationException("Business time could not be added within 400 days.");
        }
    }
}

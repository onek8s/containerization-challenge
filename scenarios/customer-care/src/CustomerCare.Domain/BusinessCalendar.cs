using System;
using System.Collections.Generic;
using System.Linq;

namespace Tailspin.CustomerCare.Domain
{
    /// <summary>
    /// Contact centre opening hours. SLA clocks only run during business hours in the
    /// contact centre's time zone and stop on public holidays.
    /// </summary>
    public class BusinessCalendar
    {
        private readonly HashSet<DateTime> _holidays;

        public BusinessCalendar(TimeZoneInfo timeZone, TimeSpan opens, TimeSpan closes, IEnumerable<DateTime> holidays)
        {
            if (timeZone == null) throw new ArgumentNullException(nameof(timeZone));
            if (closes <= opens) throw new ArgumentException("Closing time must be after opening time.");

            TimeZone = timeZone;
            Opens = opens;
            Closes = closes;
            _holidays = new HashSet<DateTime>((holidays ?? Enumerable.Empty<DateTime>()).Select(d => d.Date));
        }

        public TimeZoneInfo TimeZone { get; }
        public TimeSpan Opens { get; }
        public TimeSpan Closes { get; }

        public bool IsWorkingDay(DateTime localDate)
        {
            var d = localDate.Date;
            return d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday && !_holidays.Contains(d);
        }

        public DateTime ToLocal(DateTime utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone);
        }

        public DateTime ToUtc(DateTime local)
        {
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZone);
        }

        /// <summary>Adds working hours to a UTC instant and returns the UTC instant when they are used up.</summary>
        public DateTime AddBusinessHours(DateTime startUtc, double hours)
        {
            if (hours < 0) throw new ArgumentOutOfRangeException(nameof(hours));

            var local = ToLocal(startUtc);
            var remaining = TimeSpan.FromHours(hours);

            local = MoveToOpen(local);
            while (true)
            {
                var close = local.Date + Closes;
                var available = close - local;
                if (remaining <= available)
                {
                    return ToUtc(local + remaining);
                }

                remaining -= available;
                local = MoveToOpen(local.Date.AddDays(1) + Opens);
            }
        }

        /// <summary>Working time elapsed between two UTC instants (zero if end is before start).</summary>
        public TimeSpan BusinessTimeBetween(DateTime startUtc, DateTime endUtc)
        {
            if (endUtc <= startUtc) return TimeSpan.Zero;

            var from = ToLocal(startUtc);
            var to = ToLocal(endUtc);
            var total = TimeSpan.Zero;

            for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
            {
                if (!IsWorkingDay(day)) continue;

                var open = day + Opens;
                var close = day + Closes;
                var s = from > open ? from : open;
                var e = to < close ? to : close;
                if (e > s) total += e - s;
            }

            return total;
        }

        private DateTime MoveToOpen(DateTime local)
        {
            while (true)
            {
                if (IsWorkingDay(local))
                {
                    if (local.TimeOfDay < Opens) return local.Date + Opens;
                    if (local.TimeOfDay < Closes) return local;
                }

                local = local.Date.AddDays(1) + Opens;
            }
        }
    }
}

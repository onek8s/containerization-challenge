using System;
using Tailspin.CustomerCare.Domain;
using Xunit;

namespace Tailspin.CustomerCare.Domain.Tests
{
    public class BusinessCalendarTests
    {
        internal static BusinessCalendar Calendar(params DateTime[] holidays)
        {
            return new BusinessCalendar(TimeZoneInfo.Utc, TimeSpan.FromHours(8), TimeSpan.FromHours(17), holidays);
        }

        private static DateTime Utc(int y, int m, int d, int h, int min = 0)
        {
            return new DateTime(y, m, d, h, min, 0, DateTimeKind.Utc);
        }

        [Fact]
        public void Adds_hours_within_the_same_day()
        {
            // Thursday 29 Oct 2026
            Assert.Equal(Utc(2026, 10, 29, 13), Calendar().AddBusinessHours(Utc(2026, 10, 29, 9), 4));
        }

        [Fact]
        public void Rolls_over_to_next_morning()
        {
            Assert.Equal(Utc(2026, 10, 30, 10), Calendar().AddBusinessHours(Utc(2026, 10, 29, 15), 4));
        }

        [Fact]
        public void Skips_weekend()
        {
            // Friday 16:00 + 2h -> Monday 09:00
            Assert.Equal(Utc(2026, 11, 2, 9), Calendar().AddBusinessHours(Utc(2026, 10, 30, 16), 2));
        }

        [Fact]
        public void Skips_holidays()
        {
            var cal = Calendar(new DateTime(2026, 12, 24), new DateTime(2026, 12, 25));
            // Wednesday 23 Dec 16:00 + 2h -> Monday 28 Dec 09:00
            Assert.Equal(Utc(2026, 12, 28, 9), cal.AddBusinessHours(Utc(2026, 12, 23, 16), 2));
        }

        [Fact]
        public void Case_created_at_night_starts_when_office_opens()
        {
            Assert.Equal(Utc(2026, 10, 29, 9), Calendar().AddBusinessHours(Utc(2026, 10, 29, 2), 1));
        }

        [Fact]
        public void Business_time_between_spans_weekend()
        {
            var t = Calendar().BusinessTimeBetween(Utc(2026, 10, 30, 15), Utc(2026, 11, 2, 10));
            Assert.Equal(TimeSpan.FromHours(4), t);
        }

        [Fact]
        public void Business_time_is_zero_when_reversed()
        {
            Assert.Equal(TimeSpan.Zero, Calendar().BusinessTimeBetween(Utc(2026, 10, 30, 15), Utc(2026, 10, 30, 10)));
        }

        [Fact]
        public void Uses_local_office_hours()
        {
            var tz = TimeZoneInfo.CreateCustomTimeZone("Office+2", TimeSpan.FromHours(2), "Office+2", "Office+2");
            var cal = new BusinessCalendar(tz, TimeSpan.FromHours(8), TimeSpan.FromHours(17), null);
            // 06:00 UTC = 08:00 local, 8 hours later is 16:00 local = 14:00 UTC
            Assert.Equal(Utc(2026, 10, 29, 14), cal.AddBusinessHours(Utc(2026, 10, 29, 6), 8));
        }

        [Fact]
        public void Rejects_invalid_hours()
        {
            Assert.Throws<ArgumentException>(() => new BusinessCalendar(TimeZoneInfo.Utc, TimeSpan.FromHours(17), TimeSpan.FromHours(8), null));
        }
    }
}

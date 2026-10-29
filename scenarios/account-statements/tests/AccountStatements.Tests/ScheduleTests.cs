using System;
using System.Collections.Generic;
using Woodgrove.Statements.Core.Scheduling;
using Xunit;
using static Woodgrove.Statements.Tests.TestData;

namespace Woodgrove.Statements.Tests
{
    public class ScheduleTests
    {
        [Theory]
        [InlineData("daily 5:30", "DAILY 05:30")]
        [InlineData("WORKDAYS 06:00", "WORKDAYS 06:00")]
        [InlineData("WEEKLY thu,MON 07:30", "WEEKLY MON,THU 07:30")]
        [InlineData("MONTHLY 1 06:00", "MONTHLY 1 06:00")]
        [InlineData("MONTHLY LAST 18:00", "MONTHLY LAST 18:00")]
        [InlineData("MONTHLY WD3 06:00", "MONTHLY WD3 06:00")]
        public void Parse_and_normalise(string text, string expected)
        {
            Assert.Equal(expected, Schedule.Parse(text).ToString());
        }

        [Theory]
        [InlineData("")]
        [InlineData("HOURLY 06:00")]
        [InlineData("DAILY")]
        [InlineData("DAILY 25:00")]
        [InlineData("WEEKLY XYZ 07:00")]
        [InlineData("MONTHLY 32 06:00")]
        [InlineData("MONTHLY WD0 06:00")]
        [InlineData("WORKDAYS MON 06:00")]
        public void Invalid_schedules(string text)
        {
            Schedule s;
            string error;
            Assert.False(Schedule.TryParse(text, out s, out error));
            Assert.False(string.IsNullOrEmpty(error));
        }

        [Fact]
        public void Daily_runs_later_today_or_tomorrow()
        {
            var s = Schedule.Parse("DAILY 05:30");
            Assert.Equal(new DateTime(2026, 10, 29, 5, 30, 0), s.NextRun(new DateTime(2026, 10, 29, 1, 0, 0)));
            Assert.Equal(new DateTime(2026, 10, 30, 5, 30, 0), s.NextRun(new DateTime(2026, 10, 29, 5, 30, 0)));
        }

        [Fact]
        public void Workdays_skip_weekend_and_holidays()
        {
            var s = Schedule.Parse("WORKDAYS 06:00");
            var holidays = new HashSet<DateTime> { D(2026, 11, 2) };
            // Friday 30.10.2026 after the run -> Monday 2.11. is a holiday -> Tuesday 3.11.
            Assert.Equal(new DateTime(2026, 11, 3, 6, 0, 0), s.NextRun(new DateTime(2026, 10, 30, 7, 0, 0), holidays));
        }

        [Fact]
        public void Weekly_on_several_days()
        {
            var s = Schedule.Parse("WEEKLY MON,THU 07:00");
            Assert.Equal(new DateTime(2026, 10, 29, 7, 0, 0), s.NextRun(new DateTime(2026, 10, 27, 12, 0, 0)));
            Assert.Equal(new DateTime(2026, 11, 2, 7, 0, 0), s.NextRun(new DateTime(2026, 10, 29, 7, 0, 0)));
        }

        [Fact]
        public void Monthly_day_is_clamped_to_month_end()
        {
            var s = Schedule.Parse("MONTHLY 31 06:00");
            Assert.Equal(new DateTime(2027, 2, 28, 6, 0, 0), s.NextRun(new DateTime(2027, 2, 1)));
            Assert.Equal(new DateTime(2027, 3, 31, 6, 0, 0), s.NextRun(new DateTime(2027, 2, 28, 6, 0, 0)));
        }

        [Fact]
        public void Monthly_last_day()
        {
            var s = Schedule.Parse("MONTHLY LAST 18:00");
            Assert.Equal(new DateTime(2026, 10, 31, 18, 0, 0), s.NextRun(new DateTime(2026, 10, 29)));
            Assert.Equal(new DateTime(2026, 11, 30, 18, 0, 0), s.NextRun(new DateTime(2026, 10, 31, 18, 0, 0)));
        }

        [Fact]
        public void Third_working_day_skips_weekends_and_holidays()
        {
            var s = Schedule.Parse("MONTHLY WD3 06:00");
            var holidays = new HashSet<DateTime> { D(2027, 1, 1), D(2027, 1, 6) };
            // November 2026: Mon 2, Tue 3, Wed 4 -> 4.11.
            Assert.Equal(new DateTime(2026, 11, 4, 6, 0, 0), s.NextRun(new DateTime(2026, 10, 29), holidays));
            // January 2027: Fri 1 holiday, Mon 4, Tue 5, Wed 6 holiday, Thu 7 -> 7.1.
            Assert.Equal(new DateTime(2027, 1, 7, 6, 0, 0), s.NextRun(new DateTime(2026, 12, 31), holidays));
        }

        [Fact]
        public void Describe_is_readable()
        {
            Assert.Equal("Every Monday, Thursday at 07:30", Schedule.Parse("WEEKLY MON,THU 07:30").Describe());
            Assert.Equal("Working day 3 of the month at 06:00", Schedule.Parse("MONTHLY WD3 06:00").Describe());
        }
    }
}

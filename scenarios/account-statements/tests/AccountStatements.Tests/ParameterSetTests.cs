using System;
using Woodgrove.Statements.Core.Scheduling;
using Xunit;

namespace Woodgrove.Statements.Tests
{
    public class ParameterSetTests
    {
        static readonly DateTime Run = new DateTime(2026, 10, 5, 6, 0, 0); // Monday

        [Theory]
        [InlineData("{Today}", "2026-10-05")]
        [InlineData("{Yesterday}", "2026-10-04")]
        [InlineData("{MonthStart}", "2026-10-01")]
        [InlineData("{PrevMonthStart}", "2026-09-01")]
        [InlineData("{PrevMonthEnd}", "2026-09-30")]
        [InlineData("{WeekStart}", "2026-10-05")]
        [InlineData("{PrevWeekStart}", "2026-09-28")]
        [InlineData("{YearStart}", "2026-01-01")]
        [InlineData("{QuarterStart}", "2026-10-01")]
        [InlineData("{PrevQuarterEnd}", "2026-09-30")]
        [InlineData("{Today-7}", "2026-09-28")]
        [InlineData("{PrevMonthEnd+14}", "2026-10-14")]
        [InlineData("Mikko Laine", "Mikko Laine")]
        public void Tokens_resolve_against_the_run_date(string value, string expected)
        {
            Assert.Equal(expected, ParameterSet.ResolveValue(value, Run));
        }

        [Fact]
        public void Week_start_on_sunday_is_the_previous_monday()
        {
            Assert.Equal("2026-10-05", ParameterSet.ResolveValue("{WeekStart}", new DateTime(2026, 10, 11)));
        }

        [Fact]
        public void Unknown_token_fails()
        {
            Assert.Throws<FormatException>(() => ParameterSet.ResolveValue("{Tomorrow}", Run));
        }

        [Fact]
        public void Parse_resolve_and_round_trip_with_escapes()
        {
            var p = ParameterSet.Parse(@"PeriodStart={PrevMonthStart}; StatementDate={PrevMonthEnd};Note=a\;b\\c");
            Assert.Equal(@"a;b\c", p["note"]);
            var resolved = p.Resolve(Run);
            Assert.Equal(new DateTime(2026, 9, 30), resolved.GetDate("StatementDate", DateTime.MinValue));
            Assert.Equal(new DateTime(2000, 1, 1), resolved.GetDate("Missing", new DateTime(2000, 1, 1)));
            Assert.Equal(p.ToString(), ParameterSet.Parse(p.ToString()).ToString());
        }

        [Fact]
        public void Missing_equals_sign_fails()
        {
            Assert.Throws<FormatException>(() => ParameterSet.Parse("AsOf"));
        }

        [Fact]
        public void Non_date_value_fails_as_date()
        {
            Assert.Throws<FormatException>(() => ParameterSet.Parse("AsOf=30.9.2026").GetDate("AsOf", Run));
        }
    }
}

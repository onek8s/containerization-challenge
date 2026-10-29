using FluentAssertions;
using VanArsdel.StoreOps.Api.Domain;
using Xunit;

namespace VanArsdel.UnitTests.StoreOps;

public class OpeningHoursTests
{
    private const string Hours = "Mon-Fri 10-20; Sat 10-18; Sun 12-16";

    [Theory]
    [InlineData("2026-10-28 09:59", false)] // Wednesday
    [InlineData("2026-10-28 10:00", true)]
    [InlineData("2026-10-28 19:59", true)]
    [InlineData("2026-10-28 20:00", false)]
    [InlineData("2026-10-31 17:30", true)] // Saturday
    [InlineData("2026-10-31 18:30", false)]
    [InlineData("2026-11-01 12:00", true)] // Sunday
    public void Open_by_day_and_time(string local, bool open)
    {
        OpeningHours.IsOpen(Hours, DateTime.Parse(local, System.Globalization.CultureInfo.InvariantCulture)).Should().Be(open);
    }

    [Fact]
    public void Missing_day_means_closed()
    {
        OpeningHours.IsOpen("Mon-Fri 10-20", new DateTime(2026, 11, 1, 12, 0, 0)).Should().BeFalse();
    }

    [Fact]
    public void Minutes_are_supported()
    {
        OpeningHours.IsOpen("Mon-Sun 9:30-21.00", new DateTime(2026, 11, 1, 9, 45, 0)).Should().BeTrue();
    }

    [Fact]
    public void Range_can_wrap_over_the_week_start()
    {
        OpeningHours.IsOpen("Sat-Mon 10-16", new DateTime(2026, 11, 1, 11, 0, 0)).Should().BeTrue();
        OpeningHours.IsOpen("Sat-Mon 10-16", new DateTime(2026, 10, 28, 11, 0, 0)).Should().BeFalse();
    }
}

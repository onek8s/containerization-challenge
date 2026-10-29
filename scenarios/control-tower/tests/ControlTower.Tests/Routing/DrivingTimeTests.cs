using Proseware.ControlTower.Routing;
using Xunit;

namespace Proseware.ControlTower.Tests.Routing;

public class DrivingTimeTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 29, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Short_drive_needs_no_break()
    {
        var (arrival, state) = DrivingTime.Drive(Start, TimeSpan.FromHours(3), DriverState.Rested);
        Assert.Equal(Start.AddHours(3), arrival);
        Assert.Equal(TimeSpan.FromHours(3), state.DrivenSinceBreak);
    }

    [Fact]
    public void Break_after_four_and_a_half_hours()
    {
        var (arrival, _) = DrivingTime.Drive(Start, TimeSpan.FromHours(6), DriverState.Rested);
        Assert.Equal(Start.AddHours(6).AddMinutes(45), arrival);
    }

    [Fact]
    public void Daily_rest_after_nine_hours()
    {
        // 4.5 + 45 min + 4.5 = 9 h driven, 11 h rest, then 3 h
        var (arrival, state) = DrivingTime.Drive(Start, TimeSpan.FromHours(12), DriverState.Rested);
        Assert.Equal(Start.AddHours(9).AddMinutes(45).AddHours(11).AddHours(3), arrival);
        Assert.Equal(TimeSpan.FromHours(3), state.DrivenToday);
    }

    [Fact]
    public void Existing_driving_counts_towards_the_break()
    {
        var (arrival, _) = DrivingTime.Drive(Start, TimeSpan.FromHours(1), new DriverState(TimeSpan.FromHours(4), TimeSpan.FromHours(4)));
        Assert.Equal(Start.AddHours(1).AddMinutes(45), arrival);
    }

    [Fact]
    public void Driver_state_estimate_from_departure()
    {
        var state = DrivingTime.EstimateFromDeparture(Start, Start.AddHours(2));
        Assert.Equal(TimeSpan.FromHours(2), state.DrivenSinceBreak);
        Assert.Equal(DriverState.Rested, DrivingTime.EstimateFromDeparture(Start, Start.AddMinutes(-5)));
    }
}

namespace Proseware.ControlTower.Routing;

/// <summary>Driver state at a point in time (EU regulation 561/2006, simplified).</summary>
public sealed record DriverState(TimeSpan DrivenSinceBreak, TimeSpan DrivenToday)
{
    public static readonly DriverState Rested = new(TimeSpan.Zero, TimeSpan.Zero);
}

public static class DrivingTime
{
    public static readonly TimeSpan MaxContinuous = TimeSpan.FromHours(4.5);
    public static readonly TimeSpan Break = TimeSpan.FromMinutes(45);
    public static readonly TimeSpan MaxDaily = TimeSpan.FromHours(9);
    public static readonly TimeSpan DailyRest = TimeSpan.FromHours(11);

    /// <summary>Adds driving time to a start time, inserting breaks and daily rests.</summary>
    public static (DateTimeOffset Arrival, DriverState State) Drive(DateTimeOffset start, TimeSpan driving, DriverState state)
    {
        var now = start;
        var sinceBreak = state.DrivenSinceBreak;
        var today = state.DrivenToday;
        var remaining = driving;
        while (remaining > TimeSpan.Zero)
        {
            if (today >= MaxDaily)
            {
                now += DailyRest;
                today = TimeSpan.Zero;
                sinceBreak = TimeSpan.Zero;
                continue;
            }
            if (sinceBreak >= MaxContinuous)
            {
                now += Break;
                sinceBreak = TimeSpan.Zero;
                continue;
            }
            var chunk = Min(remaining, MaxContinuous - sinceBreak, MaxDaily - today);
            now += chunk;
            remaining -= chunk;
            sinceBreak += chunk;
            today += chunk;
        }
        return (now, new DriverState(sinceBreak, today));
    }

    /// <summary>Best guess of the driver state when a vehicle has been under way since <paramref name="departed"/>.</summary>
    public static DriverState EstimateFromDeparture(DateTimeOffset departed, DateTimeOffset now)
    {
        var elapsed = now - departed;
        if (elapsed <= TimeSpan.Zero) return DriverState.Rested;
        // replay the elapsed time as driving with mandatory pauses
        var cycle = MaxContinuous + Break;
        var dayCycle = MaxDaily + Break + DailyRest;
        var inDay = TimeSpan.FromTicks(elapsed.Ticks % dayCycle.Ticks);
        if (inDay > MaxDaily + Break) return DriverState.Rested;
        var drivenToday = inDay > MaxContinuous + Break ? inDay - Break : Min(inDay, MaxContinuous);
        var sinceBreak = TimeSpan.FromTicks(inDay.Ticks % cycle.Ticks);
        if (sinceBreak > MaxContinuous) sinceBreak = TimeSpan.Zero;
        return new DriverState(sinceBreak, Min(drivenToday, MaxDaily));
    }

    private static TimeSpan Min(params TimeSpan[] values) => values.Min();
}

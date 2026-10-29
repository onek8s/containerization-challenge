using ShiftClock.Shared;

namespace ShiftClock.Tests;

public class PunchRulesTests
{
    private static readonly DateTime Morning = new(2026, 3, 2, 6, 0, 0);
    private readonly PunchRules _rules = new();

    private static Punch PunchAt(DateTime at, PunchDirection direction, string employee = "1001") => new()
    {
        BadgeId = "0F00A1B2C3",
        EmployeeNumber = employee,
        EmployeeName = "Test Employee " + employee,
        Timestamp = at,
        Direction = direction
    };

    [Fact]
    public void First_punch_is_in()
    {
        Assert.Equal(PunchDecision.Accepted, _rules.Evaluate(null, Morning, out var direction));
        Assert.Equal(PunchDirection.In, direction);
    }

    [Fact]
    public void Punch_after_in_is_out()
    {
        _rules.Evaluate(PunchAt(Morning, PunchDirection.In), Morning.AddHours(8), out var direction);
        Assert.Equal(PunchDirection.Out, direction);
    }

    [Fact]
    public void Repeated_swipe_is_duplicate()
    {
        var decision = _rules.Evaluate(PunchAt(Morning, PunchDirection.In), Morning.AddSeconds(20), out _);
        Assert.Equal(PunchDecision.Duplicate, decision);
    }

    [Fact]
    public void Missed_clock_out_starts_new_shift()
    {
        _rules.Evaluate(PunchAt(Morning, PunchDirection.In), Morning.AddHours(24), out var direction);
        Assert.Equal(PunchDirection.In, direction);
    }

    [Fact]
    public void Summarize_pairs_punches_and_counts_unpaired()
    {
        var punches = new[]
        {
            PunchAt(Morning, PunchDirection.In),
            PunchAt(Morning.AddHours(8), PunchDirection.Out),
            PunchAt(Morning.AddDays(1), PunchDirection.In),
            PunchAt(Morning.AddDays(1).AddHours(7.5), PunchDirection.Out),
            PunchAt(Morning.AddDays(2), PunchDirection.In),
            PunchAt(Morning.AddHours(14), PunchDirection.In, "1002"),
            PunchAt(Morning.AddHours(22), PunchDirection.Out, "1002"),
        };

        var summary = PunchRules.Summarize(punches);

        Assert.Equal(2, summary.Count);
        Assert.Equal(15.5, summary[0].Hours);
        Assert.Equal(1, summary[0].UnpairedPunches);
        Assert.Equal(8, summary[1].Hours);
        Assert.Equal(0, summary[1].UnpairedPunches);
    }
}

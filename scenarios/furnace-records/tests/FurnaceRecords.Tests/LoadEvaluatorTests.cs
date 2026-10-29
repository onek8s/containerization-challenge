using FurnaceRecords.Core;

namespace FurnaceRecords.Tests;

public class LoadEvaluatorTests
{
    private static readonly DateTime T0 = new(2026, 3, 2, 6, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private static readonly RecipeSpec Recipe = new()
    {
        Code = "ST-7075",
        Name = "Solution treat 7075",
        SetpointCelsius = 480,
        ToleranceMinusCelsius = 6,
        TolerancePlusCelsius = 6,
        SoakMinutes = 50,
        MaxRampCelsiusPerMinute = 15,
        MaxQuenchDelaySeconds = 15,
    };

    private static readonly EvaluationChannel[] Channels =
    {
        new(0, ChannelRole.Control),
        new(4, ChannelRole.Load),
        new(5, ChannelRole.Load),
    };

    /// <summary>Two load sensors heating at 10 °C/min (the second 3 minutes behind) then holding.</summary>
    private static List<Sample> Curve(int totalMinutes, Func<int, DateTime, double, double>? adjust = null, TimeSpan? skipFrom = null, TimeSpan? skipTo = null)
    {
        var list = new List<Sample>();
        for (var t = TimeSpan.Zero; t <= TimeSpan.FromMinutes(totalMinutes); t += Interval)
        {
            if (skipFrom is not null && t > skipFrom && t < skipTo) continue;
            var at = T0 + t;
            var m = t.TotalMinutes;
            foreach (var (ch, lag) in new[] { (0, 0.0), (4, 1.0), (5, 3.0) })
            {
                var c = Math.Min(480, 20 + 10 * Math.Max(0, m - lag));
                list.Add(new Sample(at, ch, adjust?.Invoke(ch, at, c) ?? c));
            }
        }
        return list;
    }

    private static List<LoadEvent> Events(int doorMinute, int? quenchSecondsAfterDoor = 8)
    {
        var door = T0.AddMinutes(doorMinute);
        var list = new List<LoadEvent> { new(T0, LoadEventKind.CycleStart), new(door, LoadEventKind.DoorOpen) };
        if (quenchSecondsAfterDoor is { } q) list.Add(new(door.AddSeconds(q), LoadEventKind.QuenchStart));
        list.Add(new(door.AddMinutes(2), LoadEventKind.CycleEnd));
        return list;
    }

    [Fact]
    public void Passes_a_good_load()
    {
        var e = LoadEvaluator.Evaluate(Recipe, 2, Channels, Curve(110), Events(110), Interval);
        Assert.True(e.Passed, string.Join("; ", e.Findings.Select(f => f.Message)));
        // Coldest sensor reaches 474 °C at 3 + 45.4 min, rounded up to the record interval.
        Assert.Equal(T0.AddMinutes(48.5), e.SoakStartUtc);
        Assert.Equal(T0.AddMinutes(110), e.SoakEndUtc);
        Assert.Equal(61.5, e.SoakMinutes);
        Assert.Equal(8, e.QuenchDelaySeconds);
        Assert.Equal(10, e.MaxRampCelsiusPerMinute!.Value, 1);
        Assert.Equal(2.0, e.LeadingSensorLagMinutes);
    }

    [Fact]
    public void Short_soak_fails() =>
        Assert.Contains(LoadEvaluator.Evaluate(Recipe, 2, Channels, Curve(90), Events(90), Interval).Findings, f => f.Code == "SHORT");

    [Fact]
    public void Dip_below_lower_limit_during_soak_fails()
    {
        var dip = T0.AddMinutes(80);
        var samples = Curve(110, (ch, at, c) => ch == 5 && at >= dip && at < dip.AddMinutes(2) ? 471 : c);
        var e = LoadEvaluator.Evaluate(Recipe, 2, Channels, samples, Events(110), Interval);
        var f = Assert.Single(e.Findings, f => f.Code == "UNDER");
        Assert.Equal(5, f.Channel);
        Assert.Equal(dip, f.AtUtc);
        Assert.False(e.Passed);
    }

    [Fact]
    public void Overshoot_fails()
    {
        var samples = Curve(110, (ch, at, c) => ch == 4 && at == T0.AddMinutes(60) ? 487 : c);
        Assert.Contains(LoadEvaluator.Evaluate(Recipe, 2, Channels, samples, Events(110), Interval).Findings, f => f.Code == "OVER");
    }

    [Fact]
    public void Control_sensor_outside_band_is_not_a_load_finding()
    {
        var samples = Curve(110, (ch, at, c) => ch == 0 && at == T0.AddMinutes(60) ? 495 : c);
        Assert.True(LoadEvaluator.Evaluate(Recipe, 2, Channels, samples, Events(110), Interval).Passed);
    }

    [Fact]
    public void Slow_quench_fails()
    {
        var e = LoadEvaluator.Evaluate(Recipe, 2, Channels, Curve(110), Events(110, 22), Interval);
        Assert.Contains(e.Findings, f => f.Code == "QDELAY");
        Assert.Equal(22, e.QuenchDelaySeconds);
    }

    [Fact]
    public void Missing_quench_fails() =>
        Assert.Contains(LoadEvaluator.Evaluate(Recipe, 2, Channels, Curve(110), Events(110, null), Interval).Findings, f => f.Code == "QUENCH");

    [Fact]
    public void Recording_gap_during_soak_fails() =>
        Assert.Contains(LoadEvaluator.Evaluate(Recipe, 2, Channels, Curve(110, skipFrom: TimeSpan.FromMinutes(70), skipTo: TimeSpan.FromMinutes(72)), Events(110), Interval).Findings,
            f => f.Code == "GAP");

    [Fact]
    public void Fast_heating_fails()
    {
        var fast = Recipe with { MaxRampCelsiusPerMinute = 8 };
        Assert.Contains(LoadEvaluator.Evaluate(fast, 2, Channels, Curve(110), Events(110), Interval).Findings, f => f.Code == "RAMP");
    }

    [Fact]
    public void Furnace_class_too_wide_fails() =>
        Assert.Contains(LoadEvaluator.Evaluate(Recipe, 3, Channels, Curve(110), Events(110), Interval).Findings, f => f.Code == "CLASS");

    [Fact]
    public void Never_reaching_temperature_fails()
    {
        var e = LoadEvaluator.Evaluate(Recipe, 2, Channels, Curve(40), Events(40), Interval);
        Assert.Contains(e.Findings, f => f.Code == "NOSOAK");
        Assert.Null(e.SoakStartUtc);
    }

    [Fact]
    public void Sensor_fault_during_soak_fails()
    {
        var events = Events(110);
        events.Add(new LoadEvent(T0.AddMinutes(70), LoadEventKind.SensorFault, "Channel 5 open circuit"));
        Assert.Contains(LoadEvaluator.Evaluate(Recipe, 2, Channels, Curve(110), events, Interval).Findings, f => f.Code == "SENSOR");
    }

    [Fact]
    public void Without_load_sensors_furnace_sensors_are_used()
    {
        var e = LoadEvaluator.Evaluate(Recipe with { MaxQuenchDelaySeconds = null }, 2, new[] { new EvaluationChannel(0, ChannelRole.Control) },
            Curve(110), Events(110), Interval);
        Assert.Contains(e.Findings, f => f.Code == "NOLOAD" && f.Severity == FindingSeverity.Info);
        Assert.True(e.Passed);
    }
}

public class SampleAggregatorTests
{
    [Fact]
    public void Averages_per_interval_and_channel()
    {
        var agg = new SampleAggregator(TimeSpan.FromSeconds(10));
        var t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Empty(agg.Add(t, 1, 100));
        Assert.Empty(agg.Add(t.AddSeconds(5), 1, 102));
        Assert.Empty(agg.Add(t.AddSeconds(5), 2, 50));
        var done = agg.Add(t.AddSeconds(10), 1, 200);
        Assert.Equal(new[] { new Sample(t, 1, 101), new Sample(t, 2, 50) }, done);
        Assert.Equal(new[] { new Sample(t.AddSeconds(10), 1, 200) }, agg.Flush());
        Assert.Empty(agg.Flush());
    }
}

public class SampleCsvTests
{
    [Fact]
    public void Writes_one_row_per_time_in_local_time()
    {
        var t = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);
        var csv = SampleCsv.Write(
            new[] { new Sample(t, 0, 480.04), new Sample(t, 4, 478.5), new Sample(t.AddSeconds(10), 0, 480.1) },
            new Dictionary<int, string> { [0] = "Control", [4] = "Load; 1" },
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"));
        var lines = csv.TrimEnd().Split(Environment.NewLine);
        Assert.Equal("Time;Control;\"Load; 1\"", lines[0]);
        Assert.Equal("2026-07-01 12:00:00;480.0;478.5", lines[1]);
        Assert.Equal("2026-07-01 12:00:10;480.1;", lines[2]);
    }
}

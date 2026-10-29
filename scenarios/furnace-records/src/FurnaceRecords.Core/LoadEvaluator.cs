namespace FurnaceRecords.Core;

public enum FindingSeverity
{
    Info,
    Fail,
}

public sealed record Finding(FindingSeverity Severity, string Code, string Message, DateTime? AtUtc = null, int? Channel = null);

public sealed record ChannelSummary(int Channel, ChannelRole Role, double Min, double Max, double? SoakMin, double? SoakMax);

public sealed record LoadEvaluation
{
    public required bool Passed { get; init; }
    public DateTime? SoakStartUtc { get; init; }
    public DateTime? SoakEndUtc { get; init; }
    public double SoakMinutes { get; init; }
    public double? MaxRampCelsiusPerMinute { get; init; }
    public double? QuenchDelaySeconds { get; init; }
    public double? LeadingSensorLagMinutes { get; init; }
    public required IReadOnlyList<ChannelSummary> Channels { get; init; }
    public required IReadOnlyList<Finding> Findings { get; init; }
}

public sealed record EvaluationChannel(int Channel, ChannelRole Role);

/// <summary>
/// Checks a recorded load against its recipe: soak start when the coldest load sensor reaches the
/// lower limit, all load sensors within tolerance until the end of soak, soak time, heating rate
/// and quench delay.
/// </summary>
public static class LoadEvaluator
{
    public static LoadEvaluation Evaluate(
        RecipeSpec recipe,
        int furnaceClass,
        IReadOnlyList<EvaluationChannel> channels,
        IReadOnlyList<Sample> samples,
        IReadOnlyList<LoadEvent> events,
        TimeSpan recordInterval)
    {
        var findings = new List<Finding>();

        if (!FurnaceClass.CanRun(furnaceClass, Math.Min(recipe.ToleranceMinusCelsius, recipe.TolerancePlusCelsius)))
        {
            findings.Add(new(FindingSeverity.Fail, "CLASS",
                $"Furnace class {furnaceClass} (±{FurnaceClass.UniformityTolerance(furnaceClass)} °C) is not qualified for recipe tolerance -{recipe.ToleranceMinusCelsius}/+{recipe.TolerancePlusCelsius} °C."));
        }

        var loadChannels = channels.Where(c => c.Role == ChannelRole.Load).Select(c => c.Channel).ToHashSet();
        if (loadChannels.Count == 0)
        {
            // Without load sensors the recorder sensors stand for the load.
            loadChannels = channels.Where(c => c.Role is ChannelRole.Recorder or ChannelRole.Control).Select(c => c.Channel).ToHashSet();
            findings.Add(new(FindingSeverity.Info, "NOLOAD", "No load sensors; soak evaluated on furnace sensors."));
        }

        // Rows: one timestamp with the value of every load channel present.
        var byTime = samples
            .Where(s => loadChannels.Contains(s.Channel))
            .GroupBy(s => s.AtUtc)
            .OrderBy(g => g.Key)
            .Select(g => (At: g.Key, Values: g.ToDictionary(s => s.Channel, s => s.Celsius)))
            .Where(r => r.Values.Count == loadChannels.Count)
            .ToList();

        var doorOpen = events.Where(e => e.Kind == LoadEventKind.DoorOpen).Select(e => (DateTime?)e.AtUtc).ToList();
        var cycleEnd = events.Where(e => e.Kind == LoadEventKind.CycleEnd).Select(e => (DateTime?)e.AtUtc).LastOrDefault();

        DateTime? soakStart = null;
        DateTime? soakEnd = null;
        foreach (var row in byTime)
        {
            if (row.Values.Values.Min() >= recipe.LowerLimit)
            {
                soakStart = row.At;
                break;
            }
        }

        if (soakStart is null)
        {
            findings.Add(new(FindingSeverity.Fail, "NOSOAK", $"Load sensors never all reached {recipe.LowerLimit:0.#} °C."));
        }
        else
        {
            soakEnd = doorOpen.FirstOrDefault(d => d > soakStart) ?? cycleEnd ?? byTime[^1].At;
            var start = soakStart.Value;
            var end = soakEnd.Value;
            var soakRows = byTime.Where(r => r.At >= start && r.At <= end).ToList();

            foreach (var ch in loadChannels.OrderBy(c => c))
            {
                var below = soakRows.FirstOrDefault(r => r.Values[ch] < recipe.LowerLimit);
                if (below.Values is not null)
                {
                    findings.Add(new(FindingSeverity.Fail, "UNDER",
                        $"Channel {ch} at {below.Values[ch]:0.0} °C below {recipe.LowerLimit:0.#} °C during soak.", below.At, ch));
                }
                var above = soakRows.FirstOrDefault(r => r.Values[ch] > recipe.UpperLimit);
                if (above.Values is not null)
                {
                    findings.Add(new(FindingSeverity.Fail, "OVER",
                        $"Channel {ch} at {above.Values[ch]:0.0} °C above {recipe.UpperLimit:0.#} °C during soak.", above.At, ch));
                }
            }

            var maxGap = TimeSpan.FromTicks(recordInterval.Ticks * 3);
            for (var i = 1; i < soakRows.Count; i++)
            {
                if (soakRows[i].At - soakRows[i - 1].At > maxGap)
                {
                    findings.Add(new(FindingSeverity.Fail, "GAP",
                        $"No load sensor data for {(soakRows[i].At - soakRows[i - 1].At).TotalSeconds:0} s during soak.", soakRows[i - 1].At));
                }
            }

            var soakMinutes = (end - start).TotalMinutes;
            if (soakMinutes + 1e-9 < recipe.SoakMinutes)
            {
                findings.Add(new(FindingSeverity.Fail, "SHORT", $"Soak {soakMinutes:0.0} min is shorter than {recipe.SoakMinutes} min."));
            }
            if (recipe.MaxSoakMinutes is { } maxSoak && soakMinutes > maxSoak)
            {
                findings.Add(new(FindingSeverity.Fail, "LONG", $"Soak {soakMinutes:0.0} min is longer than {maxSoak} min."));
            }

            foreach (var e in events.Where(e => e.Kind is LoadEventKind.SensorFault && e.AtUtc >= start && e.AtUtc <= end))
            {
                findings.Add(new(FindingSeverity.Fail, "SENSOR", $"Sensor fault during soak: {e.Detail}", e.AtUtc));
            }
            foreach (var e in events.Where(e => e.Kind is LoadEventKind.Alarm && e.AtUtc >= start && e.AtUtc <= end))
            {
                findings.Add(new(FindingSeverity.Info, "ALARM", $"Furnace alarm during soak: {e.Detail}", e.AtUtc));
            }
        }

        // Heating rate over one-minute windows up to soak start.
        double? maxRamp = null;
        var rampRows = byTime.Where(r => soakStart is null || r.At <= soakStart).ToList();
        var j = 0;
        for (var i = 0; i < rampRows.Count; i++)
        {
            while (j < i && rampRows[i].At - rampRows[j].At > TimeSpan.FromMinutes(1)) j++;
            var minutes = (rampRows[i].At - rampRows[j].At).TotalMinutes;
            if (minutes < 0.999) continue;
            foreach (var ch in loadChannels)
            {
                var rate = (rampRows[i].Values[ch] - rampRows[j].Values[ch]) / minutes;
                if (maxRamp is null || rate > maxRamp) maxRamp = rate;
            }
        }
        if (recipe.MaxRampCelsiusPerMinute is { } maxRate && maxRamp > maxRate)
        {
            findings.Add(new(FindingSeverity.Fail, "RAMP", $"Heating rate {maxRamp:0.0} °C/min exceeds {maxRate:0.#} °C/min."));
        }

        // Quench delay.
        double? quenchDelay = null;
        if (recipe.MaxQuenchDelaySeconds is { } maxDelay)
        {
            var open = soakEnd is null ? null : doorOpen.FirstOrDefault(d => d >= soakEnd);
            var quench = open is null ? null : events.Where(e => e.Kind == LoadEventKind.QuenchStart && e.AtUtc >= open).Select(e => (DateTime?)e.AtUtc).FirstOrDefault();
            if (open is null || quench is null)
            {
                findings.Add(new(FindingSeverity.Fail, "QUENCH", "Door open and quench not recorded after soak."));
            }
            else
            {
                quenchDelay = (quench.Value - open.Value).TotalSeconds;
                if (quenchDelay > maxDelay)
                {
                    findings.Add(new(FindingSeverity.Fail, "QDELAY", $"Quench delay {quenchDelay:0.0} s exceeds {maxDelay} s.", quench));
                }
            }
        }

        // Time between the first and the last load sensor reaching the lower limit.
        double? lag = null;
        if (soakStart is not null)
        {
            var first = byTime.FirstOrDefault(r => r.Values.Values.Max() >= recipe.LowerLimit);
            lag = (soakStart.Value - first.At).TotalMinutes;
        }

        var summaries = channels
            .OrderBy(c => c.Channel)
            .Select(c =>
            {
                var values = samples.Where(s => s.Channel == c.Channel).ToList();
                var soakValues = soakStart is null
                    ? new List<Sample>()
                    : values.Where(s => s.AtUtc >= soakStart && s.AtUtc <= soakEnd).ToList();
                return new ChannelSummary(
                    c.Channel, c.Role,
                    values.Count == 0 ? double.NaN : values.Min(s => s.Celsius),
                    values.Count == 0 ? double.NaN : values.Max(s => s.Celsius),
                    soakValues.Count == 0 ? null : soakValues.Min(s => s.Celsius),
                    soakValues.Count == 0 ? null : soakValues.Max(s => s.Celsius));
            })
            .ToList();

        return new LoadEvaluation
        {
            Passed = findings.All(f => f.Severity != FindingSeverity.Fail),
            SoakStartUtc = soakStart,
            SoakEndUtc = soakEnd,
            SoakMinutes = soakStart is null ? 0 : Math.Round((soakEnd!.Value - soakStart.Value).TotalMinutes, 1),
            MaxRampCelsiusPerMinute = maxRamp is null ? null : Math.Round(maxRamp.Value, 1),
            QuenchDelaySeconds = quenchDelay is null ? null : Math.Round(quenchDelay.Value, 1),
            LeadingSensorLagMinutes = lag is null ? null : Math.Round(lag.Value, 1),
            Channels = summaries,
            Findings = findings,
        };
    }
}

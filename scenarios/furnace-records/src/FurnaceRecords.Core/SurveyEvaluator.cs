namespace FurnaceRecords.Core;

public sealed record SurveySensorResult(int Channel, double Min, double Max, double MaxDeviation);

public sealed record SurveyEvaluation
{
    public required bool Passed { get; init; }
    public required double ToleranceCelsius { get; init; }
    public DateTime? StabilisedUtc { get; init; }
    public double SurveyMinutes { get; init; }
    public double? UniformityCelsius { get; init; }
    public int? HotChannel { get; init; }
    public int? ColdChannel { get; init; }
    public required IReadOnlyList<SurveySensorResult> Sensors { get; init; }
    public required IReadOnlyList<Finding> Findings { get; init; }
}

/// <summary>
/// Temperature uniformity survey at one temperature: after all survey sensors are within the class
/// tolerance (stabilised), every reading of every survey sensor must stay within tolerance for the
/// survey time.
/// </summary>
public static class SurveyEvaluator
{
    public const int MinimumSensors = 5;
    public const int SurveyMinutesRequired = 30;
    public static readonly TimeSpan MaxRecordInterval = TimeSpan.FromMinutes(2);

    public static SurveyEvaluation Evaluate(double surveyCelsius, int furnaceClass, IReadOnlyList<int> surveyChannels,
        IReadOnlyList<Sample> samples, DateTime? endUtc, TimeSpan recordInterval)
    {
        var tolerance = FurnaceClass.UniformityTolerance(furnaceClass);
        var findings = new List<Finding>();
        if (surveyChannels.Count < MinimumSensors)
        {
            findings.Add(new(FindingSeverity.Fail, "SENSORS", $"{surveyChannels.Count} survey sensors; at least {MinimumSensors} are required."));
        }
        if (recordInterval > MaxRecordInterval)
        {
            findings.Add(new(FindingSeverity.Fail, "INTERVAL", $"Record interval {recordInterval.TotalSeconds:0} s is longer than {MaxRecordInterval.TotalSeconds:0} s."));
        }

        var set = surveyChannels.ToHashSet();
        var rows = samples.Where(s => set.Contains(s.Channel))
            .GroupBy(s => s.AtUtc).OrderBy(g => g.Key)
            .Select(g => (At: g.Key, Values: g.ToDictionary(s => s.Channel, s => s.Celsius)))
            .Where(r => r.Values.Count == set.Count)
            .ToList();

        bool InBand(double v) => Math.Abs(v - surveyCelsius) <= tolerance + 1e-9;
        var start = rows.FirstOrDefault(r => r.Values.Values.All(InBand));
        if (start.Values is null)
        {
            findings.Add(new(FindingSeverity.Fail, "NOTSTABLE", $"Survey sensors never all within ±{tolerance} °C of {surveyCelsius} °C."));
            return new SurveyEvaluation { Passed = false, ToleranceCelsius = tolerance, Sensors = Array.Empty<SurveySensorResult>(), Findings = findings };
        }

        var end = endUtc ?? rows[^1].At;
        var window = rows.Where(r => r.At >= start.At && r.At <= end).ToList();
        var minutes = (end - start.At).TotalMinutes;
        if (minutes + 1e-9 < SurveyMinutesRequired)
        {
            findings.Add(new(FindingSeverity.Fail, "SHORT", $"Survey {minutes:0.0} min after stabilisation; {SurveyMinutesRequired} min are required."));
        }

        var sensors = surveyChannels.OrderBy(c => c).Select(ch =>
        {
            var values = window.Select(r => r.Values[ch]).ToList();
            var min = values.Min();
            var max = values.Max();
            return new SurveySensorResult(ch, min, max, Math.Round(Math.Max(Math.Abs(max - surveyCelsius), Math.Abs(min - surveyCelsius)), 1));
        }).ToList();

        foreach (var s in sensors.Where(s => s.MaxDeviation > tolerance + 1e-9))
        {
            findings.Add(new(FindingSeverity.Fail, "UNIFORMITY", $"Survey sensor {s.Channel} deviates {s.MaxDeviation:0.0} °C (tolerance ±{tolerance} °C).", null, s.Channel));
        }

        var hot = sensors.MaxBy(s => s.Max)!;
        var cold = sensors.MinBy(s => s.Min)!;
        return new SurveyEvaluation
        {
            Passed = findings.All(f => f.Severity != FindingSeverity.Fail),
            ToleranceCelsius = tolerance,
            StabilisedUtc = start.At,
            SurveyMinutes = Math.Round(minutes, 1),
            UniformityCelsius = Math.Round(hot.Max - cold.Min, 1),
            HotChannel = hot.Channel,
            ColdChannel = cold.Channel,
            Sensors = sensors,
            Findings = findings,
        };
    }

    /// <summary>Months until the next survey for a passed survey.</summary>
    public static int IntervalMonths(int furnaceClass, InstrumentationType type) => (furnaceClass, type) switch
    {
        ( <= 2, InstrumentationType.A or InstrumentationType.B) => 3,
        ( <= 2, _) => 1,
        (_, InstrumentationType.A or InstrumentationType.B) => 6,
        _ => 3,
    };
}

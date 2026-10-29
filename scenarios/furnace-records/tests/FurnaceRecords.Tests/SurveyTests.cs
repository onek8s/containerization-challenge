using System.Text;
using FurnaceRecords.Core;

namespace FurnaceRecords.Tests;

public class SurveyEvaluatorTests
{
    private static readonly DateTime T0 = new(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly int[] Channels = { 8, 9, 10, 11, 12, 13, 14, 15, 16 };

    private static List<Sample> Survey(int minutes, Func<int, double, double>? offset = null)
    {
        var list = new List<Sample>();
        for (var s = 0; s <= minutes * 60; s += 10)
        {
            var m = s / 60.0;
            foreach (var ch in Channels)
            {
                var t = Math.Min(480 + (ch - 12) * 0.8, 20 + 12 * Math.Max(0, m - (16 - ch) * 0.5));
                list.Add(new Sample(T0.AddSeconds(s), ch, t + (offset?.Invoke(ch, m) ?? 0)));
            }
        }
        return list;
    }

    [Fact]
    public void Uniform_furnace_passes()
    {
        var e = SurveyEvaluator.Evaluate(480, 2, Channels, Survey(90), null, TimeSpan.FromSeconds(10));
        Assert.True(e.Passed, string.Join("; ", e.Findings.Select(f => f.Message)));
        Assert.Equal(6, e.ToleranceCelsius);
        Assert.Equal(16, e.HotChannel);
        Assert.Equal(8, e.ColdChannel);
        Assert.InRange(e.UniformityCelsius!.Value, 6.4, 9.2);
        Assert.True(e.SurveyMinutes >= 30);
        Assert.Equal(9, e.Sensors.Count);
    }

    [Fact]
    public void Hot_spot_fails()
    {
        var e = SurveyEvaluator.Evaluate(480, 2, Channels, Survey(90, (ch, m) => ch == 13 && m > 70 ? 6 : 0), null, TimeSpan.FromSeconds(10));
        var f = Assert.Single(e.Findings);
        Assert.Equal("UNIFORMITY", f.Code);
        Assert.Equal(13, f.Channel);
    }

    [Fact]
    public void Class_3_tolerance_accepts_the_same_hot_spot() =>
        Assert.True(SurveyEvaluator.Evaluate(480, 3, Channels, Survey(90, (ch, m) => ch == 13 && m > 70 ? 6 : 0), null, TimeSpan.FromSeconds(10)).Passed);

    [Fact]
    public void Too_few_sensors_short_survey_and_long_interval_fail()
    {
        var e = SurveyEvaluator.Evaluate(480, 2, Channels[..4], Survey(60), null, TimeSpan.FromMinutes(3));
        Assert.Contains(e.Findings, f => f.Code == "SENSORS");
        Assert.Contains(e.Findings, f => f.Code == "SHORT");
        Assert.Contains(e.Findings, f => f.Code == "INTERVAL");
    }

    [Fact]
    public void Never_stable_fails() =>
        Assert.Contains(SurveyEvaluator.Evaluate(480, 2, Channels, Survey(20), null, TimeSpan.FromSeconds(10)).Findings, f => f.Code == "NOTSTABLE");

    [Theory]
    [InlineData(2, InstrumentationType.B, 3)]
    [InlineData(2, InstrumentationType.C, 1)]
    [InlineData(3, InstrumentationType.A, 6)]
    [InlineData(5, InstrumentationType.D, 3)]
    public void Survey_intervals(int cls, InstrumentationType type, int months) =>
        Assert.Equal(months, SurveyEvaluator.IntervalMonths(cls, type));
}

public class SimplePdfTests
{
    [Fact]
    public void Writes_valid_cross_reference_table()
    {
        var pdf = new SimplePdf();
        pdf.Line("Heat Treatment Certificate (HT-1)", 16, bold: true);
        for (var i = 0; i < 80; i++) pdf.Line($"Line {i} 480 °C ±6");
        var bytes = pdf.ToArray();
        var text = Encoding.Latin1.GetString(bytes);

        Assert.StartsWith("%PDF-1.4", text);
        Assert.EndsWith("%%EOF\n", text);
        Assert.Contains("/Count 2", text);
        Assert.Contains(@"(Heat Treatment Certificate \(HT-1\)) Tj", text);

        var startxref = int.Parse(text[(text.LastIndexOf("startxref\n", StringComparison.Ordinal) + 10)..].Split('\n')[0]);
        Assert.StartsWith("xref", text[startxref..]);
        var entries = text[startxref..].Split('\n').Skip(3).TakeWhile(l => l.EndsWith(" n ")).ToList();
        Assert.Equal(8, entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            var offset = int.Parse(entries[i][..10]);
            Assert.StartsWith($"{i + 1} 0 obj", text[offset..]);
        }
    }
}

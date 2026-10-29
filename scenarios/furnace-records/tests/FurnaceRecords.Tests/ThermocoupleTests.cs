using FurnaceRecords.Core;

namespace FurnaceRecords.Tests;

public class ThermocoupleTests
{
    // NIST ITS-90 table values, mV.
    [Theory]
    [InlineData(ThermocoupleType.K, 0, 0.000)]
    [InlineData(ThermocoupleType.K, 100, 4.096)]
    [InlineData(ThermocoupleType.K, 500, 20.644)]
    [InlineData(ThermocoupleType.K, 1000, 41.276)]
    [InlineData(ThermocoupleType.K, -100, -3.554)]
    [InlineData(ThermocoupleType.N, 100, 2.774)]
    [InlineData(ThermocoupleType.N, 500, 16.748)]
    [InlineData(ThermocoupleType.N, 1000, 36.256)]
    public void Emf_matches_reference_tables(ThermocoupleType type, double celsius, double millivolts) =>
        Assert.Equal(millivolts, Thermocouple.ToMillivolts(type, celsius), 3);

    [Theory]
    [InlineData(ThermocoupleType.K)]
    [InlineData(ThermocoupleType.N)]
    public void Inverse_round_trips(ThermocoupleType type)
    {
        for (var t = -50.0; t <= 1250; t += 37.5)
        {
            Assert.Equal(t, Thermocouple.ToCelsius(type, Thermocouple.ToMillivolts(type, t)), 2);
        }
    }

    [Fact]
    public void Cold_junction_compensation_adds_terminal_emf()
    {
        var measured = (Thermocouple.ToMillivolts(ThermocoupleType.K, 480) - Thermocouple.ToMillivolts(ThermocoupleType.K, 25)) * 1000;
        Assert.Equal(480, Thermocouple.Compensate(ThermocoupleType.K, measured, 25), 2);
    }

    [Fact]
    public void Out_of_range_emf_is_clamped() =>
        Assert.Equal(1372, Thermocouple.ToCelsius(ThermocoupleType.K, 80));
}

public class SensorCorrectionTests
{
    private static readonly CorrectionPoint[] Points = { new(480, -0.4), new(120, 0.2) };

    [Theory]
    [InlineData(50, 50.2)]
    [InlineData(120, 120.2)]
    [InlineData(300, 299.9)]
    [InlineData(480, 479.6)]
    [InlineData(600, 599.6)]
    public void Interpolates_between_points(double reading, double expected) =>
        Assert.Equal(expected, SensorCorrection.Apply(Points, reading), 6);

    [Fact]
    public void No_points_means_no_correction() => Assert.Equal(480, SensorCorrection.Apply(Array.Empty<CorrectionPoint>(), 480));

    [Fact]
    public void Rejects_large_and_duplicate_corrections()
    {
        var errors = SensorCorrection.Validate(new[] { new CorrectionPoint(480, 2.5), new CorrectionPoint(480, 0.1) });
        Assert.Equal(2, errors.Count);
    }
}

public class FurnaceClassTests
{
    [Theory]
    [InlineData(2, 6, true)]
    [InlineData(3, 6, false)]
    [InlineData(3, 8, true)]
    [InlineData(1, 3, true)]
    public void Class_must_be_at_least_as_tight_as_recipe(int cls, double tolerance, bool expected) =>
        Assert.Equal(expected, FurnaceClass.CanRun(cls, tolerance));

    [Fact]
    public void Unknown_class_throws() => Assert.Throws<ArgumentOutOfRangeException>(() => FurnaceClass.UniformityTolerance(7));
}

public class LoadNumberTests
{
    [Fact]
    public void Formats_and_validates()
    {
        var n = LoadNumbers.Format("F03", 2026, 412);
        Assert.Equal("F03-26-00412", n);
        Assert.True(LoadNumbers.IsValid(n));
        Assert.False(LoadNumbers.IsValid("f03-26-412"));
    }
}

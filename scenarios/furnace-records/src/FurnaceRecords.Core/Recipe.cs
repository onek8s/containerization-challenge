namespace FurnaceRecords.Core;

/// <summary>Heat treatment recipe as used for evaluating a load.</summary>
public sealed record RecipeSpec
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Specification { get; init; }

    /// <summary>Soak setpoint, °C.</summary>
    public required double SetpointCelsius { get; init; }

    public required double ToleranceMinusCelsius { get; init; }
    public required double TolerancePlusCelsius { get; init; }

    /// <summary>Minimum time all load sensors must be within tolerance.</summary>
    public required int SoakMinutes { get; init; }

    /// <summary>Maximum soak; longer soaks are reported but do not fail the load unless set.</summary>
    public int? MaxSoakMinutes { get; init; }

    /// <summary>Maximum heating rate of any load sensor, °C per minute.</summary>
    public double? MaxRampCelsiusPerMinute { get; init; }

    /// <summary>Maximum time from door open to quench, seconds (solution treatment).</summary>
    public int? MaxQuenchDelaySeconds { get; init; }

    public double LowerLimit => SetpointCelsius - ToleranceMinusCelsius;
    public double UpperLimit => SetpointCelsius + TolerancePlusCelsius;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Code)) errors.Add("Code is required.");
        if (SetpointCelsius is < 50 or > 1250) errors.Add("Setpoint must be between 50 and 1250 °C.");
        if (ToleranceMinusCelsius <= 0 || TolerancePlusCelsius <= 0) errors.Add("Tolerances must be positive.");
        if (SoakMinutes is <= 0 or > 2880) errors.Add("Soak must be between 1 and 2880 minutes.");
        if (MaxSoakMinutes is { } max && max < SoakMinutes) errors.Add("Maximum soak must not be shorter than the soak.");
        if (MaxRampCelsiusPerMinute is <= 0) errors.Add("Maximum ramp rate must be positive.");
        if (MaxQuenchDelaySeconds is <= 0) errors.Add("Maximum quench delay must be positive.");
        return errors;
    }
}

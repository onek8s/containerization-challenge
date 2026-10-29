namespace FurnaceRecords.Core;

/// <summary>Correction from a sensor or instrument calibration at one temperature.</summary>
public sealed record CorrectionPoint(double AtCelsius, double CorrectionCelsius);

public static class SensorCorrection
{
    /// <summary>Largest correction accepted for a sensor used on production loads.</summary>
    public const double MaxCorrectionCelsius = 2.2;

    /// <summary>
    /// Applies the correction for the reading, interpolated linearly between calibration points
    /// and held constant outside them.
    /// </summary>
    public static double Apply(IReadOnlyList<CorrectionPoint> points, double reading)
    {
        if (points.Count == 0)
        {
            return reading;
        }

        var sorted = points.Count == 1 ? points : points.OrderBy(p => p.AtCelsius).ToList();
        if (reading <= sorted[0].AtCelsius)
        {
            return reading + sorted[0].CorrectionCelsius;
        }
        for (var i = 1; i < sorted.Count; i++)
        {
            var a = sorted[i - 1];
            var b = sorted[i];
            if (reading <= b.AtCelsius)
            {
                var f = (reading - a.AtCelsius) / (b.AtCelsius - a.AtCelsius);
                return reading + a.CorrectionCelsius + f * (b.CorrectionCelsius - a.CorrectionCelsius);
            }
        }
        return reading + sorted[^1].CorrectionCelsius;
    }

    public static IReadOnlyList<string> Validate(IReadOnlyList<CorrectionPoint> points)
    {
        var errors = new List<string>();
        if (points.Select(p => p.AtCelsius).Distinct().Count() != points.Count)
        {
            errors.Add("Calibration temperatures must be unique.");
        }
        foreach (var p in points.Where(p => Math.Abs(p.CorrectionCelsius) > MaxCorrectionCelsius))
        {
            errors.Add($"Correction {p.CorrectionCelsius:+0.0;-0.0} °C at {p.AtCelsius:0} °C exceeds ±{MaxCorrectionCelsius} °C.");
        }
        return errors;
    }
}

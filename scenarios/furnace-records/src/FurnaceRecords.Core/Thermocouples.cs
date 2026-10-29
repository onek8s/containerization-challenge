namespace FurnaceRecords.Core;

public enum ThermocoupleType
{
    K,
    N,
}

/// <summary>
/// ITS-90 reference functions for base metal thermocouples (NIST Monograph 175).
/// EMF in millivolts, temperature in degrees Celsius.
/// </summary>
public static class Thermocouple
{
    // Type K, 0 °C to 1372 °C, with the exponential term.
    private static readonly double[] KPositive =
    {
        -0.176004136860E-01, 0.389212049750E-01, 0.185587700320E-04, -0.994575928740E-07,
        0.318409457190E-09, -0.560728448890E-12, 0.560750590590E-15, -0.320207200030E-18,
        0.971511471520E-22, -0.121047212750E-25,
    };

    // Type K, -270 °C to 0 °C.
    private static readonly double[] KNegative =
    {
        0.0, 0.394501280250E-01, 0.236223735980E-04, -0.328589067840E-06,
        -0.499048287770E-08, -0.675090591730E-10, -0.574103274280E-12, -0.310888728940E-14,
        -0.104516093650E-16, -0.198892668780E-19, -0.163226974860E-22,
    };

    private const double KA0 = 0.118597600000E+00;
    private const double KA1 = -0.118343200000E-03;
    private const double KA2 = 0.126968600000E+03;

    // Type N, 0 °C to 1300 °C.
    private static readonly double[] NPositive =
    {
        0.0, 0.259293946010E-01, 0.157101418800E-04, 0.438256272370E-07,
        -0.252611697940E-09, 0.643118193390E-12, -0.100634715190E-14, 0.997453389920E-18,
        -0.608632456070E-21, 0.208492293390E-24, -0.306821961510E-28,
    };

    // Type N, -270 °C to 0 °C.
    private static readonly double[] NNegative =
    {
        0.0, 0.261591059620E-01, 0.109574842280E-04, -0.938411115540E-07,
        -0.464120397590E-10, -0.263033577160E-11, -0.226534380030E-13, -0.760893007910E-16,
        -0.934196678350E-19,
    };

    public static double MinCelsius(ThermocoupleType type) => -270.0;

    public static double MaxCelsius(ThermocoupleType type) => type switch
    {
        ThermocoupleType.K => 1372.0,
        ThermocoupleType.N => 1300.0,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static double ToMillivolts(ThermocoupleType type, double celsius)
    {
        switch (type)
        {
            case ThermocoupleType.K:
                if (celsius < 0)
                {
                    return Polynomial(KNegative, celsius);
                }
                var d = celsius - KA2;
                return Polynomial(KPositive, celsius) + KA0 * Math.Exp(KA1 * d * d);
            case ThermocoupleType.N:
                return Polynomial(celsius < 0 ? NNegative : NPositive, celsius);
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    /// <summary>
    /// Temperature for an EMF. The reference function is inverted numerically so the result is
    /// consistent with <see cref="ToMillivolts"/> to well below 0.01 °C.
    /// </summary>
    public static double ToCelsius(ThermocoupleType type, double millivolts)
    {
        var lo = MinCelsius(type);
        var hi = MaxCelsius(type);
        if (millivolts <= ToMillivolts(type, lo)) return lo;
        if (millivolts >= ToMillivolts(type, hi)) return hi;

        // Newton steps from a linear first guess, bisection as a fallback.
        var t = millivolts / (type == ThermocoupleType.K ? 0.041 : 0.036);
        for (var i = 0; i < 30; i++)
        {
            var e = ToMillivolts(type, t) - millivolts;
            if (Math.Abs(e) < 1e-7)
            {
                return t;
            }
            if (e > 0) hi = Math.Min(hi, t); else lo = Math.Max(lo, t);
            var slope = (ToMillivolts(type, t + 0.01) - ToMillivolts(type, t - 0.01)) / 0.02;
            var next = t - e / slope;
            t = next > lo && next < hi ? next : (lo + hi) / 2;
        }
        return t;
    }

    /// <summary>
    /// Hot junction temperature from the measured EMF and the cold junction (terminal block) temperature.
    /// </summary>
    public static double Compensate(ThermocoupleType type, double measuredMicrovolts, double coldJunctionCelsius)
    {
        var total = measuredMicrovolts / 1000.0 + ToMillivolts(type, coldJunctionCelsius);
        return ToCelsius(type, total);
    }

    private static double Polynomial(double[] c, double x)
    {
        var sum = 0.0;
        for (var i = c.Length - 1; i >= 0; i--)
        {
            sum = sum * x + c[i];
        }
        return sum;
    }
}

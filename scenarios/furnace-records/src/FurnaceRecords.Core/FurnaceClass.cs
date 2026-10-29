namespace FurnaceRecords.Core;

/// <summary>Furnace class with its temperature uniformity tolerance.</summary>
public static class FurnaceClass
{
    private static readonly Dictionary<int, double> Tolerances = new()
    {
        [1] = 3.0,
        [2] = 6.0,
        [3] = 8.0,
        [4] = 10.0,
        [5] = 14.0,
        [6] = 28.0,
    };

    public static IReadOnlyCollection<int> All => Tolerances.Keys;

    public static double UniformityTolerance(int furnaceClass) =>
        Tolerances.TryGetValue(furnaceClass, out var t)
            ? t
            : throw new ArgumentOutOfRangeException(nameof(furnaceClass), furnaceClass, "Furnace class must be 1 to 6.");

    /// <summary>A furnace may only run recipes whose tolerance is not tighter than its class.</summary>
    public static bool CanRun(int furnaceClass, double recipeToleranceCelsius) =>
        UniformityTolerance(furnaceClass) <= recipeToleranceCelsius + 1e-9;
}

public enum InstrumentationType
{
    A,
    B,
    C,
    D,
}

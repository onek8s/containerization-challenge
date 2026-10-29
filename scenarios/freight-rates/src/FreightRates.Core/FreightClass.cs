namespace FreightRates.Core;

public static class FreightClass
{
    public static readonly IReadOnlyList<decimal> All = new[]
    {
        50m, 55m, 60m, 65m, 70m, 77.5m, 85m, 92.5m, 100m, 110m,
        125m, 150m, 175m, 200m, 250m, 300m, 400m, 500m,
    };

    public static bool IsValid(decimal freightClass) => All.Contains(freightClass);

    // Density based classification (pounds per cubic foot), NMFC Item 133300 scale.
    private static readonly (decimal MinPcf, decimal Class)[] DensityScale =
    {
        (50m, 50m), (35m, 55m), (30m, 60m), (22.5m, 65m), (15m, 70m),
        (13.5m, 77.5m), (12m, 85m), (10.5m, 92.5m), (9m, 100m), (8m, 110m),
        (7m, 125m), (6m, 150m), (5m, 175m), (4m, 200m), (3m, 250m),
        (2m, 300m), (1m, 400m), (0m, 500m),
    };

    public static decimal DensityPcf(int weightLbs, decimal lengthIn, decimal widthIn, decimal heightIn)
    {
        if (lengthIn <= 0 || widthIn <= 0 || heightIn <= 0)
            throw new ArgumentOutOfRangeException(nameof(lengthIn), "Dimensions must be positive.");
        var cubicFeet = lengthIn * widthIn * heightIn / 1728m;
        return Math.Round(weightLbs / cubicFeet, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal FromDensity(decimal pcf)
    {
        foreach (var (minPcf, cls) in DensityScale)
        {
            if (pcf >= minPcf)
                return cls;
        }
        return 500m;
    }

    public static decimal Resolve(ShipmentLine line)
    {
        if (line.FreightClass is { } cls)
            return cls;
        if (line.LengthIn is { } l && line.WidthIn is { } w && line.HeightIn is { } h)
            return FromDensity(DensityPcf(line.WeightLbs, l * line.Pieces, w, h));
        throw new RatingException(RatingErrorKind.InvalidShipment, 0, "Freight class or dimensions are required for every line.");
    }
}

using FreightRates.Core;

namespace FreightRates.Tests;

public class FreightClassTests
{
    [Theory]
    [InlineData(55.0, 50)]
    [InlineData(50.0, 50)]
    [InlineData(15.0, 70)]
    [InlineData(9.0, 100)]
    [InlineData(8.99, 110)]
    [InlineData(2.0, 300)]
    [InlineData(0.5, 500)]
    public void FromDensity_uses_density_scale(decimal pcf, decimal expected)
    {
        Assert.Equal(expected, FreightClass.FromDensity(pcf));
    }

    [Fact]
    public void DensityPcf_computes_pounds_per_cubic_foot()
    {
        // 48 x 40 x 40 in = 44.44 cu ft
        Assert.Equal(11.25m, FreightClass.DensityPcf(500, 48, 40, 40));
    }

    [Fact]
    public void Resolve_prefers_declared_class()
    {
        var line = new ShipmentLine { FreightClass = 85m, WeightLbs = 300, LengthIn = 48, WidthIn = 40, HeightIn = 40 };
        Assert.Equal(85m, FreightClass.Resolve(line));
    }

    [Fact]
    public void Resolve_uses_dimensions_and_pieces()
    {
        var line = new ShipmentLine { WeightLbs = 1000, Pieces = 2, LengthIn = 48, WidthIn = 40, HeightIn = 40 };
        // 1000 lbs over 88.89 cu ft = 11.25 pcf -> class 92.5
        Assert.Equal(92.5m, FreightClass.Resolve(line));
    }

    [Fact]
    public void Resolve_without_class_or_dimensions_fails()
    {
        var ex = Assert.Throws<RatingException>(() => FreightClass.Resolve(new ShipmentLine { WeightLbs = 100 }));
        Assert.Equal(RatingErrorKind.InvalidShipment, ex.Kind);
    }
}

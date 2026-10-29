using FreightRates.Core;

namespace FreightRates.Tests;

public class ShipmentValidatorTests
{
    private static readonly DateOnly Today = new(2026, 10, 29);

    private static ShipmentRequest Valid() => new()
    {
        OriginZip = "60601",
        DestinationZip = "30301",
        ShipDate = Today.AddDays(1),
        Lines = new[] { new ShipmentLine { FreightClass = 70m, WeightLbs = 1200 } },
    };

    [Fact]
    public void Valid_shipment_has_no_errors() =>
        Assert.Empty(ShipmentValidator.Validate(Valid(), Today));

    [Fact]
    public void Rejects_bad_zip_and_class()
    {
        var s = Valid() with
        {
            OriginZip = "6060A",
            Lines = new[] { new ShipmentLine { FreightClass = 71m, WeightLbs = 100 } },
        };
        var errors = ShipmentValidator.Validate(s, Today);
        Assert.Contains(errors, e => e.StartsWith("Origin ZIP"));
        Assert.Contains(errors, e => e.Contains("71 is not a valid freight class"));
    }

    [Fact]
    public void Rejects_overweight_shipment()
    {
        var s = Valid() with
        {
            Lines = new[]
            {
                new ShipmentLine { FreightClass = 70m, WeightLbs = 15000 },
                new ShipmentLine { FreightClass = 70m, WeightLbs = 6000 },
            },
        };
        Assert.Contains(ShipmentValidator.Validate(s, Today), e => e.StartsWith("Total weight exceeds"));
    }

    [Fact]
    public void Rejects_too_many_lines()
    {
        var s = Valid() with
        {
            Lines = Enumerable.Range(0, 21).Select(_ => new ShipmentLine { FreightClass = 70m, WeightLbs = 100 }).ToList(),
        };
        Assert.Contains(ShipmentValidator.Validate(s, Today), e => e.Contains("at most 20 lines"));
    }

    [Fact]
    public void Line_needs_class_or_all_dimensions()
    {
        var s = Valid() with { Lines = new[] { new ShipmentLine { WeightLbs = 100, LengthIn = 48, WidthIn = 40 } } };
        Assert.Contains(ShipmentValidator.Validate(s, Today), e => e.Contains("freight class or all dimensions"));
    }

    [Theory]
    [InlineData(-31, true)]
    [InlineData(-30, false)]
    [InlineData(180, false)]
    [InlineData(181, true)]
    public void Ship_date_window(int days, bool invalid)
    {
        var errors = ShipmentValidator.Validate(Valid() with { ShipDate = Today.AddDays(days) }, Today);
        Assert.Equal(invalid, errors.Any(e => e.StartsWith("Ship date")));
    }
}

using FreightRates.Core;

namespace FreightRates.Tests;

public class QuoteCalculatorTests
{
    private static EngineRateResult Rate(decimal gross, decimal minimum = 100m) => new()
    {
        GrossCharge = gross,
        TariffMinimumCharge = minimum,
    };

    [Fact]
    public void Applies_discount_and_fuel_surcharge()
    {
        var q = QuoteCalculator.Calculate(Rate(1000m), new CustomerPricing(70m, 0m), 30m, 1500, Array.Empty<AccessorialRate>());
        Assert.Equal(700m, q.Discount);
        Assert.Equal(300m, q.NetLinehaul);
        Assert.Equal(90m, q.FuelSurcharge);
        Assert.Equal(390m, q.Total);
        Assert.False(q.MinimumApplied);
    }

    [Fact]
    public void Tariff_minimum_applies_when_customer_has_none()
    {
        var q = QuoteCalculator.Calculate(Rate(200m, 110m), new CustomerPricing(70m, 0m), 0m, 200, Array.Empty<AccessorialRate>());
        Assert.True(q.MinimumApplied);
        Assert.Equal(110m, q.NetLinehaul);
    }

    [Fact]
    public void Customer_minimum_overrides_tariff_minimum()
    {
        var q = QuoteCalculator.Calculate(Rate(200m, 110m), new CustomerPricing(70m, 95m), 0m, 200, Array.Empty<AccessorialRate>());
        Assert.True(q.MinimumApplied);
        Assert.Equal(95m, q.NetLinehaul);
    }

    [Fact]
    public void Customer_fuel_override_is_used()
    {
        var q = QuoteCalculator.Calculate(Rate(1000m), new CustomerPricing(50m, 0m, 24m), 31.5m, 1000, Array.Empty<AccessorialRate>());
        Assert.Equal(24m, q.FuelSurchargePercent);
        Assert.Equal(120m, q.FuelSurcharge);
    }

    [Fact]
    public void Per_cwt_accessorial_has_minimum_amount()
    {
        var acc = new[]
        {
            new AccessorialRate("INDL", "Inside delivery", 85m, 6.50m),
            new AccessorialRate("LGDL", "Liftgate delivery", 75m),
        };
        var light = QuoteCalculator.Calculate(Rate(1000m), new CustomerPricing(0m, 0m), 0m, 500, acc);
        var heavy = QuoteCalculator.Calculate(Rate(1000m), new CustomerPricing(0m, 0m), 0m, 2000, acc);
        Assert.Equal(85m + 75m, light.AccessorialTotal);
        Assert.Equal(130m + 75m, heavy.AccessorialTotal);
    }

    [Fact]
    public void Rounds_to_cents()
    {
        var q = QuoteCalculator.Calculate(Rate(333.33m), new CustomerPricing(33.33m, 0m), 17.5m, 100, Array.Empty<AccessorialRate>());
        Assert.Equal(111.10m, q.Discount);
        Assert.Equal(222.23m, q.NetLinehaul);
        Assert.Equal(38.89m, q.FuelSurcharge);
    }

    [Fact]
    public void Rejects_unreasonable_discount() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            QuoteCalculator.Calculate(Rate(100m), new CustomerPricing(96m, 0m), 0m, 100, Array.Empty<AccessorialRate>()));
}

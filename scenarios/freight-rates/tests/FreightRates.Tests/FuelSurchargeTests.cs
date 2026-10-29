using FreightRates.Core;

namespace FreightRates.Tests;

public class FuelSurchargeTests
{
    private static readonly FuelSurchargeStep[] Table =
    {
        new(2.50m, 20m), new(2.55m, 20.5m), new(2.60m, 21m),
    };

    [Theory]
    [InlineData(2.49, 0)]
    [InlineData(2.50, 20)]
    [InlineData(2.549, 20)]
    [InlineData(2.55, 20.5)]
    [InlineData(4.00, 21)]
    public void PercentFor_uses_step_table(decimal price, decimal expected) =>
        Assert.Equal(expected, FuelSurcharge.PercentFor(price, Table));

    [Theory]
    [InlineData("2026-10-26", "2026-10-26")]
    [InlineData("2026-10-29", "2026-10-26")]
    [InlineData("2026-11-01", "2026-10-26")]
    public void WeekOf_returns_monday(string date, string monday) =>
        Assert.Equal(DateOnly.Parse(monday), FuelSurcharge.WeekOf(DateOnly.Parse(date)));
}

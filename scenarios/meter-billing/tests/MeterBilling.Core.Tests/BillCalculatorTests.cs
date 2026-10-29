using Coho.Billing.Core.Rating;
using Xunit;

namespace Coho.Billing.Core.Tests;

public class BillCalculatorTests
{
    private readonly BillCalculator _calculator = new BillCalculator();

    private Bill Calculate(DateTime from, DateTime to, decimal consumption, string tariff = "DOM") =>
        _calculator.Calculate(TestData.Account(tariff: tariff),
            new BillingPeriod { From = from, To = to, OpeningReading = 100m, ClosingReading = 100m + consumption, Consumption = consumption },
            TestData.Tariffs(), new DateTime(2026, 5, 3), 14);

    [Fact]
    public void Single_tariff_period_in_first_block()
    {
        var bill = Calculate(new DateTime(2026, 3, 1), new DateTime(2026, 5, 30), 30m);

        Assert.Equal(3, bill.Lines.Count);
        Assert.Equal(20.52m, bill.Lines.Single(l => l.Code == "BASIC").Amount);
        Assert.Equal(43.80m, bill.Lines.Single(l => l.Code == "WATER1").Amount);
        Assert.Equal(76.80m, bill.Lines.Single(l => l.Code == "WASTE").Amount);
        Assert.Equal(141.12m, bill.NetAmount);
        Assert.Equal(35.99m, bill.VatAmount);
        Assert.Equal(177.11m, bill.TotalAmount);
        Assert.Equal(new DateTime(2026, 5, 17), bill.DueDate);
    }

    [Fact]
    public void Blocks_are_scaled_to_the_days_in_the_period()
    {
        var bill = Calculate(new DateTime(2026, 3, 1), new DateTime(2026, 4, 30), 70m);

        var water = bill.Lines.Where(l => l.Code.StartsWith("WATER")).ToList();
        Assert.Equal(new[] { 20m, 30m, 20m }, water.Select(l => l.Quantity));
        Assert.Equal(new[] { 29.20m, 54.90m, 43.80m }, water.Select(l => l.Amount));
        Assert.Equal("Water, block 2", water[1].Description);
    }

    [Fact]
    public void Tariff_change_splits_the_period_by_days()
    {
        var bill = Calculate(new DateTime(2026, 1, 28), new DateTime(2026, 4, 28), 30.7m);

        var basic = bill.Lines.Where(l => l.Code == "BASIC").ToList();
        Assert.Equal(new[] { 32m, 58m }, basic.Select(l => l.Quantity));
        Assert.Equal(new[] { 0.2137m, 0.228m }, basic.Select(l => l.UnitPrice));
        Assert.EndsWith(" 28.01.-28.02.2026", basic[0].Description);
        Assert.EndsWith(" 01.03.-27.04.2026", basic[1].Description);

        var water = bill.Lines.Where(l => l.Code.StartsWith("WATER")).ToList();
        Assert.Equal(30.7m, water.Sum(l => l.Quantity));
        Assert.Equal(10.667m, water[0].Quantity);
        Assert.Equal(1.38m, water[0].UnitPrice);

        var waste = bill.Lines.Where(l => l.Code == "WASTE").ToList();
        Assert.Equal(new[] { 10.916m, 19.784m }, waste.Select(l => l.Quantity));
        Assert.Equal(bill.NetAmount + bill.VatAmount, bill.TotalAmount);
    }

    [Fact]
    public void No_sewer_connection_has_no_wastewater_line()
    {
        var bill = Calculate(new DateTime(2026, 1, 29), new DateTime(2026, 4, 28), 24.8m, "DOMNS");

        Assert.DoesNotContain(bill.Lines, l => l.Code == "WASTE");
        Assert.Equal(24.8m, bill.Lines.Single(l => l.Code.StartsWith("WATER")).Quantity);
    }

    [Fact]
    public void Missing_tariff_is_an_error()
    {
        Assert.Throws<InvalidOperationException>(() => Calculate(new DateTime(2024, 6, 1), new DateTime(2024, 9, 1), 10m));
    }

    [Fact]
    public void Empty_period_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Calculate(new DateTime(2026, 4, 1), new DateTime(2026, 4, 1), 0m));
    }
}

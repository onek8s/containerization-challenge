using FluentAssertions;
using VanArsdel.Allocation.Api.Domain;
using VanArsdel.Contracts.Orders;
using VanArsdel.Contracts.Stores;
using Xunit;

namespace VanArsdel.UnitTests.Allocation;

public class AllocationEngineTests
{
    private readonly AllocationEngine _engine = new();
    private readonly AllocationRules _rules = new();

    private static StockAtLocation Dc(params (string Sku, int Qty)[] stock) => Location("DC01", LocationTypes.DistributionCentre, "FI", stock);

    private static StockAtLocation Store(string code, string country, params (string Sku, int Qty)[] stock) => Location(code, LocationTypes.Store, country, stock);

    private static StockAtLocation Location(string code, string type, string country, (string Sku, int Qty)[] stock)
    {
        var s = new StockAtLocation { LocationCode = code, LocationType = type, Country = country, ShipFromStore = true, ClickAndCollect = true };
        foreach (var (sku, qty) in stock) s.Available[sku] = qty;
        return s;
    }

    private static List<StockLineRef> Order(params (string Sku, int Qty)[] lines) =>
        lines.Select(l => new StockLineRef { Sku = l.Sku, Quantity = l.Qty }).ToList();

    [Fact]
    public void Ships_from_the_dc_when_it_has_everything()
    {
        var plan = _engine.Plan(Order(("A", 1), ("B", 2)), DeliveryMethods.Home, "FI", null,
            new[] { Dc(("A", 5), ("B", 5)), Store("FI012", "FI", ("A", 9), ("B", 9)) }, _rules);

        plan.Success.Should().BeTrue();
        plan.Strategy.Should().Be("Dc");
        plan.Shipments.Should().ContainSingle().Which.LocationCode.Should().Be("DC01");
    }

    [Fact]
    public void Ships_from_the_store_with_most_stock_when_the_dc_is_short()
    {
        var plan = _engine.Plan(Order(("A", 1)), DeliveryMethods.Home, "FI", null,
            new[] { Dc(("A", 0)), Store("FI001", "FI", ("A", 1)), Store("FI012", "FI", ("A", 4)) }, _rules);

        plan.Strategy.Should().Be("ShipFromStore");
        plan.Shipments.Single().LocationCode.Should().Be("FI012");
    }

    [Fact]
    public void Does_not_use_stores_in_another_country()
    {
        var plan = _engine.Plan(Order(("A", 1)), DeliveryMethods.Home, "FI", null,
            new[] { Dc(), Store("SE003", "SE", ("A", 4)) }, _rules);

        plan.Success.Should().BeFalse();
        plan.ShortLines.Should().ContainSingle().Which.Quantity.Should().Be(1);
    }

    [Fact]
    public void Splits_between_dc_and_a_store()
    {
        var plan = _engine.Plan(Order(("A", 1), ("B", 1)), DeliveryMethods.Home, "FI", null,
            new[] { Dc(("A", 3)), Store("FI012", "FI", ("B", 2)) }, _rules);

        plan.Success.Should().BeTrue();
        plan.Strategy.Should().Be("Split");
        plan.Shipments.Select(s => s.LocationCode).Should().Equal("DC01", "FI012");
    }

    [Fact]
    public void Split_respects_the_shipment_limit()
    {
        var plan = _engine.Plan(Order(("A", 1), ("B", 1), ("C", 1)), DeliveryMethods.Home, "FI", null,
            new[] { Dc(("A", 3)), Store("FI012", "FI", ("B", 2)), Store("FI020", "FI", ("C", 2)) }, new AllocationRules { MaxShipments = 2 });

        plan.Success.Should().BeFalse();
        plan.ShortLines.Select(l => l.Sku).Should().Equal("C");
    }

    [Fact]
    public void Store_minimum_is_kept_back()
    {
        var plan = _engine.Plan(Order(("A", 1)), DeliveryMethods.Home, "FI", null,
            new[] { Dc(), Store("FI012", "FI", ("A", 1)) }, new AllocationRules { StoreMinimumLeft = 1 });

        plan.Success.Should().BeFalse();
    }

    [Fact]
    public void Click_and_collect_uses_the_collection_store_first()
    {
        var plan = _engine.Plan(Order(("A", 1)), DeliveryMethods.ClickAndCollect, "FI", "FI012",
            new[] { Dc(("A", 10)), Store("FI012", "FI", ("A", 1)) }, _rules);

        plan.Strategy.Should().Be("CollectFromStock");
        plan.Shipments.Single().LocationCode.Should().Be("FI012");
    }

    [Fact]
    public void Click_and_collect_goes_via_the_dc_when_the_store_is_short()
    {
        var plan = _engine.Plan(Order(("A", 2)), DeliveryMethods.ClickAndCollect, "FI", "FI012",
            new[] { Dc(("A", 10)), Store("FI012", "FI", ("A", 1)) }, _rules);

        plan.Strategy.Should().Be("CollectViaDc");
    }

    [Fact]
    public void Same_sku_on_two_lines_is_added_up()
    {
        var plan = _engine.Plan(Order(("A", 1), ("a", 2)), DeliveryMethods.Home, "FI", null, new[] { Dc(("A", 2)) }, _rules);

        plan.Success.Should().BeFalse();
        plan.ShortLines.Single().Quantity.Should().Be(1);
    }
}

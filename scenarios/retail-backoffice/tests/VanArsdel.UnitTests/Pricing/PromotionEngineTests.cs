using FluentAssertions;
using VanArsdel.Pricing.Api.Domain;
using Xunit;

namespace VanArsdel.UnitTests.Pricing;

public class PromotionEngineTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
    private readonly PromotionEngine _engine = new();

    private static Promotion Promo(int id, string type, decimal value, Action<Promotion> configure = null)
    {
        var p = new Promotion
        {
            Id = id,
            Code = "P" + id,
            Name = "Promotion " + id,
            Type = type,
            Value = value,
            Markets = "FI,SE",
            Status = "Active",
            StartsUtc = Now.AddDays(-1),
            EndsUtc = Now.AddDays(7),
        };
        configure?.Invoke(p);
        return p;
    }

    private static BasketLine Line(string sku, decimal price, int qty = 1, string category = "TOPS") => new()
    {
        Sku = sku,
        StyleCode = sku.Split('-')[0],
        CategoryCode = category,
        Brand = "VanArsdel",
        Season = "AW26",
        UnitPrice = price,
        Quantity = qty,
    };

    private BasketResult Apply(IList<BasketLine> lines, params Promotion[] promotions) =>
        _engine.Apply("FI", "EUR", 0.255m, lines, promotions, Now, "Web", null);

    [Fact]
    public void Basket_without_promotions_has_no_discount_and_vat_is_included_in_price()
    {
        var result = Apply(new[] { Line("VA1001-BLK-M", 125.50m) });

        result.Total.Should().Be(125.50m);
        result.DiscountTotal.Should().Be(0);
        result.VatAmount.Should().Be(25.50m);
    }

    [Fact]
    public void Percent_off_applies_to_lines_in_scope_only()
    {
        var promo = Promo(1, PromotionTypes.PercentOff, 20, p => p.Scopes.Add(new PromotionScope { ScopeType = "Category", Value = "TOPS" }));
        var lines = new[] { Line("VA1001-BLK-M", 50m), Line("VA2001-NAV-32", 80m, category: "TROUSERS") };

        var result = Apply(lines, promo);

        lines[0].Discount.Should().Be(10m);
        lines[1].Discount.Should().Be(0);
        result.Total.Should().Be(120m);
        result.AppliedPromotions.Should().Equal("P1");
    }

    [Fact]
    public void Three_for_two_gives_the_cheapest_item_free()
    {
        var lines = new[] { Line("VA1001-BLK-M", 30m), Line("VA1002-NAV-S", 20m), Line("VA1003-RED-L", 40m) };

        var result = Apply(lines, Promo(2, PromotionTypes.BuyXPayY, 0, p => { p.BuyQuantity = 3; p.PayQuantity = 2; }));

        result.DiscountTotal.Should().Be(20m);
        lines[1].Discount.Should().Be(20m);
        result.Total.Should().Be(70m);
    }

    [Fact]
    public void Three_for_two_needs_a_full_group()
    {
        var lines = new[] { Line("VA1001-BLK-M", 30m, qty: 2) };

        var result = Apply(lines, Promo(2, PromotionTypes.BuyXPayY, 0, p => { p.BuyQuantity = 3; p.PayQuantity = 2; }));

        result.DiscountTotal.Should().Be(0);
        result.AppliedPromotions.Should().BeEmpty();
    }

    [Fact]
    public void Bundle_price_is_spread_over_the_items_by_price()
    {
        var lines = new[] { Line("VA5001-WHT-M", 30m), Line("VA5002-BLK-M", 10m) };

        var result = Apply(lines, Promo(3, PromotionTypes.Bundle, 32m, p => p.BuyQuantity = 2));

        result.Total.Should().Be(32m);
        lines[0].Discount.Should().Be(6m);
        lines[1].Discount.Should().Be(2m);
    }

    [Fact]
    public void Amount_off_below_minimum_basket_tells_how_much_is_missing()
    {
        var result = Apply(new[] { Line("VA1001-BLK-M", 80m) }, Promo(4, PromotionTypes.AmountOff, 10m, p => p.MinBasket = 100m));

        result.DiscountTotal.Should().Be(0);
        result.Messages.Should().ContainSingle().Which.Should().ContainAny("20.00", "20,00");
    }

    [Fact]
    public void Amount_off_is_split_so_the_lines_add_up_exactly()
    {
        var lines = new[] { Line("A-1", 33.33m), Line("B-1", 33.33m), Line("C-1", 33.34m) };

        var result = Apply(lines, Promo(4, PromotionTypes.AmountOff, 10m, p => p.MinBasket = 100m));

        result.DiscountTotal.Should().Be(10m);
        lines.Sum(l => l.Discount).Should().Be(10m);
        result.Total.Should().Be(90m);
    }

    [Fact]
    public void Markdown_runs_before_line_promotions_and_keeps_the_original_price()
    {
        var line = Line("VA1001-BLK-M", 100m);
        var markdown = Promo(5, PromotionTypes.Markdown, 30, p => p.Priority = 0);
        var percent = Promo(6, PromotionTypes.PercentOff, 10, p => p.Priority = 10);

        var result = Apply(new[] { line }, percent, markdown);

        line.CompareAtPrice.Should().Be(100m);
        line.Discount.Should().Be(37m);
        result.AppliedPromotions.Should().Equal("P5", "P6");
    }

    [Fact]
    public void Exclusive_promotion_locks_its_lines()
    {
        var line = Line("VA1001-BLK-M", 100m);
        var exclusive = Promo(7, PromotionTypes.PercentOff, 40, p => { p.Exclusive = true; p.Priority = 10; });
        var other = Promo(8, PromotionTypes.PercentOff, 10, p => p.Priority = 1);

        var result = Apply(new[] { line }, exclusive, other);

        line.Discount.Should().Be(40m);
        result.AppliedPromotions.Should().Equal("P7");
    }

    [Fact]
    public void Coupon_promotion_needs_the_code()
    {
        var coupon = Promo(9, PromotionTypes.PercentOff, 15, p => { p.RequiresCode = true; p.Code = "AUTUMN15"; });
        var lines = new[] { Line("VA1001-BLK-M", 100m) };

        _engine.Apply("FI", "EUR", 0.255m, lines, new[] { coupon }, Now, "Web", null).DiscountTotal.Should().Be(0);
        _engine.Apply("FI", "EUR", 0.255m, lines, new[] { coupon }, Now, "Web", "autumn15").DiscountTotal.Should().Be(15m);
    }

    [Theory]
    [InlineData("Active", "FI", "Web", true)]
    [InlineData("Draft", "FI", "Web", false)]
    [InlineData("Active", "NO", "Web", false)]
    [InlineData("Active", "SE", "Store", false)]
    public void Promotion_is_active_by_status_market_and_channel(string status, string market, string channel, bool expected)
    {
        var p = Promo(10, PromotionTypes.PercentOff, 10, x => { x.Status = status; x.Channel = "Web"; });

        p.IsActive(Now, market, channel, null).Should().Be(expected);
    }

    [Fact]
    public void Promotion_end_time_is_exclusive()
    {
        var p = Promo(11, PromotionTypes.PercentOff, 10);

        p.IsActive(p.EndsUtc.AddSeconds(-1), "FI", "Web", null).Should().BeTrue();
        p.IsActive(p.EndsUtc, "FI", "Web", null).Should().BeFalse();
    }
}

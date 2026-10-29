using ExpenseClaims.Core.Currency;

namespace ExpenseClaims.Tests.Core;

public class CurrencyTests
{
    [Fact]
    public void Ecb_feed_is_parsed()
    {
        using var file = File.OpenRead(TestFiles.Sample("ecb", "eurofxref-hist-sample.xml"));
        var rates = EcbRatesParser.Parse(file);
        Assert.Equal(15, rates.Count);
        Assert.Contains(rates, r => r.Date == new DateOnly(2022, 10, 12) && r.Currency == "SEK" && r.UnitsPerEuro == 10.9240m);
    }

    [Fact]
    public void Weekend_expense_uses_previous_business_day_rate()
    {
        var rates = new[]
        {
            new ReferenceRate(new DateOnly(2022, 10, 6), "SEK", 10.85m),
            new ReferenceRate(new DateOnly(2022, 10, 7), "SEK", 10.90m),
            new ReferenceRate(new DateOnly(2022, 10, 10), "SEK", 10.95m)
        };
        Assert.Equal(10.90m, CurrencyConverter.PickRate(rates, "SEK", new DateOnly(2022, 10, 9))!.UnitsPerEuro);
        Assert.Equal(10.95m, CurrencyConverter.PickRate(rates, "sek", new DateOnly(2022, 10, 10))!.UnitsPerEuro);
    }

    [Fact]
    public void Too_old_rate_is_not_used()
    {
        var rates = new[] { new ReferenceRate(new DateOnly(2022, 9, 1), "USD", 1.0m) };
        Assert.Null(CurrencyConverter.PickRate(rates, "USD", new DateOnly(2022, 9, 20)));
    }

    [Fact]
    public void Conversion_rounds_to_cents() =>
        Assert.Equal(151.04m, CurrencyConverter.ToEuro(1650m, 10.9240m));
}

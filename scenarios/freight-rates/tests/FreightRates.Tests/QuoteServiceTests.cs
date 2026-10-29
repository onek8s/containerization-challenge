using FreightRates.Core;
using FreightRates.Data;
using FreightRates.Data.Entities;
using FreightRates.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FreightRates.Tests;

public sealed class QuoteServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly FreightRatesContext _db;
    private readonly FakeRatingEngine _engine = new();
    private readonly FixedTime _time = new(new DateTimeOffset(2026, 10, 29, 10, 0, 0, TimeSpan.Zero));

    public QuoteServiceTests()
    {
        _connection.Open();
        _db = new FreightRatesContext(new DbContextOptionsBuilder<FreightRatesContext>().UseSqlite(_connection).Options);
        DbInitializer.InitializeAsync(_db, seedSampleData: true).GetAwaiter().GetResult();
        _db.FuelPrices.RemoveRange(_db.FuelPrices);
        _db.FuelPrices.Add(new FuelPrice { WeekStarting = new DateOnly(2026, 10, 26), DieselPrice = 3.60m });
        _db.FuelPrices.Add(new FuelPrice { WeekStarting = new DateOnly(2026, 11, 2), DieselPrice = 3.80m });
        _db.SaveChanges();
    }

    private QuoteService Service() => new(_db, _engine, _time);

    private Customer Acme() => _db.Customers.Single(c => c.Code == "ACME01");

    private static ShipmentRequest Shipment(DateOnly shipDate, params string[] accessorials) => new()
    {
        OriginZip = "60601",
        DestinationZip = "30301",
        ShipDate = shipDate,
        Lines = new[] { new ShipmentLine { FreightClass = 70m, WeightLbs = 2000 } },
        Accessorials = accessorials,
    };

    [Fact]
    public async Task Creates_numbered_quotes_per_day()
    {
        var first = await Service().CreateQuoteAsync(Acme(), Shipment(new DateOnly(2026, 10, 30)), "PO-1");
        var second = await Service().CreateQuoteAsync(Acme(), Shipment(new DateOnly(2026, 10, 30)), null);
        Assert.Equal("Q261029-0001", first.Number);
        Assert.Equal("Q261029-0002", second.Number);
        Assert.Equal(new DateOnly(2026, 11, 28), first.ValidUntil);
    }

    [Fact]
    public async Task Uses_fuel_price_of_ship_week()
    {
        // 3.60 -> 20 + 22 * 0.5 = 31 %, 3.80 -> 33 %
        Assert.Equal(31m, await Service().FuelSurchargePercentAsync(new DateOnly(2026, 11, 1)));
        Assert.Equal(33m, await Service().FuelSurchargePercentAsync(new DateOnly(2026, 11, 4)));
        Assert.Equal(0m, await Service().FuelSurchargePercentAsync(new DateOnly(2026, 1, 5)));
    }

    [Fact]
    public async Task Prices_with_customer_discount_and_accessorials()
    {
        // gross 2000 lbs x 50.00 = 1000.00; ACME01 72 % -> 280.00; fuel 31 % -> 86.80; LGDL 75
        var quote = await Service().CreateQuoteAsync(Acme(), Shipment(new DateOnly(2026, 10, 30), "lgdl"), null);
        Assert.Equal(1000m, quote.GrossCharge);
        Assert.Equal(280m, quote.NetLinehaul);
        Assert.Equal(86.80m, quote.FuelSurcharge);
        Assert.Equal(441.80m, quote.Total);
        Assert.Equal("LGDL", quote.AccessorialCodes);
        Assert.Equal("TEST0001", quote.TariffName);
    }

    [Fact]
    public async Task Unknown_accessorial_is_rejected_before_rating()
    {
        var ex = await Assert.ThrowsAsync<QuoteValidationException>(() =>
            Service().CreateQuoteAsync(Acme(), Shipment(new DateOnly(2026, 10, 30), "XYZ"), null));
        Assert.Contains("Unknown accessorial code XYZ.", ex.Errors);
        Assert.Empty(_engine.Requests);
    }

    [Fact]
    public async Task Engine_errors_are_not_stored()
    {
        _engine.Failure = new RatingException(RatingErrorKind.PointNotServed, -303, "Point not served");
        await Assert.ThrowsAsync<RatingException>(() => Service().CreateQuoteAsync(Acme(), Shipment(new DateOnly(2026, 10, 30)), null));
        Assert.Empty(_db.Quotes);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private sealed class FixedTime : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedTime(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}

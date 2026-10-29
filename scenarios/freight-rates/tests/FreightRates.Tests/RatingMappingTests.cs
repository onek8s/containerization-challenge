using FreightRates.Contracts;
using FreightRates.Core;

namespace FreightRates.Tests;

public class RatingMappingTests
{
    [Fact]
    public void Date_round_trips() =>
        Assert.Equal(new DateOnly(2026, 2, 3), RatingMapping.FromDateInt(RatingMapping.ToDateInt(new DateOnly(2026, 2, 3))));

    [Theory]
    [InlineData(77.5, 775)]
    [InlineData(92.5, 925)]
    [InlineData(500, 5000)]
    public void Class_is_sent_times_ten(decimal cls, int x10)
    {
        Assert.Equal(x10, RatingMapping.ClassToX10(cls));
        Assert.Equal(cls, RatingMapping.ClassFromX10(x10));
    }

    [Fact]
    public void Request_resolves_density_class()
    {
        var request = RatingMapping.ToRequest(new ShipmentRequest
        {
            OriginZip = "60601",
            DestinationZip = "30301",
            ShipDate = new DateOnly(2026, 11, 2),
            Lines = new[] { new ShipmentLine { WeightLbs = 500, LengthIn = 48, WidthIn = 40, HeightIn = 40 } },
        });
        Assert.Equal(20261102, request.ShipDate);
        Assert.Equal(925, request.Lines[0].FreightClassX10);
    }

    [Fact]
    public void Reply_is_converted_from_cents()
    {
        var reply = new RateReply { GrossChargeCents = 123456, MinimumChargeCents = 9500, TariffName = "CZ560200" };
        reply.Lines.Add(new RateLineResult { FreightClassX10 = 775, WeightLbs = 1000, RatePerCwtCents = 4250, ChargeCents = 42500 });
        var result = RatingMapping.ToResult(reply);
        Assert.Equal(1234.56m, result.GrossCharge);
        Assert.Equal(95m, result.TariffMinimumCharge);
        Assert.Equal(new EngineLineRate(77.5m, 1000, 42.50m, 425m), result.Lines[0]);
    }
}

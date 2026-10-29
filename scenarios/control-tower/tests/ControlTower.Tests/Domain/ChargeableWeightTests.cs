using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Pricing;
using Proseware.ControlTower.Domain.Shipments;
using Xunit;

namespace Proseware.ControlTower.Tests.Domain;

public class ChargeableWeightTests
{
    [Fact]
    public void Air_uses_volumetric_weight_rounded_to_half_kilo()
    {
        // 100 x 50 x 40 cm = 0.2 m3 -> 33.33 kg volumetric, gross 12 kg
        var p = new[] { new Package { Kind = PackageKind.Crate, WeightKg = 12, LengthCm = 100, WidthCm = 50, HeightCm = 40 } };
        Assert.Equal(33.5m, ChargeableWeight.For(TransportMode.Air, p));
    }

    [Fact]
    public void Road_uses_loading_metres_for_non_stackable_pallets()
    {
        // 4 non-stackable EUR pallets: 4 x 0.4 = 1.6 ldm -> 2960 kg
        var p = new[] { new Package { Kind = PackageKind.Pallet, Quantity = 4, WeightKg = 300, LengthCm = 120, WidthCm = 80, HeightCm = 120, Stackable = false } };
        Assert.Equal(1.6m, p[0].LoadingMeters);
        Assert.Equal(2960m, ChargeableWeight.For(TransportMode.Road, p));
    }

    [Fact]
    public void Sea_is_weight_or_measure()
    {
        var p = new[] { new Package { Kind = PackageKind.Pallet, WeightKg = 400, LengthCm = 120, WidthCm = 100, HeightCm = 150 } };
        Assert.Equal(1800m, ChargeableWeight.For(TransportMode.Sea, p));
    }

    [Fact]
    public void Quote_applies_breaks_minimum_and_surcharges()
    {
        var card = new RateCard
        {
            Id = 1, Mode = TransportMode.Road, OriginCountry = "FI", DestinationCountry = "SE", MinimumCharge = 65, FuelSurchargePercent = 10, TransitDays = 2,
            ValidFrom = new DateTime(2026, 1, 1), ValidTo = new DateTime(2026, 12, 31),
            Breaks = { new RateBreak { FromKg = 0, PricePer100Kg = 34 }, new RateBreak { FromKg = 500, PricePer100Kg = 22 } }
        };
        var packages = new[] { new Package { Kind = PackageKind.Pallet, WeightKg = 600, LengthCm = 120, WidthCm = 80, HeightCm = 100 } };
        var quote = new QuoteCalculator().Calculate(new QuoteRequest(TransportMode.Road, "FI", "33100", "SE", packages, new DateTime(2026, 10, 29), true, null), new[] { card }).Single();

        Assert.Equal(600m, quote.ChargeableKg);
        Assert.Equal(132m, quote.Lines.Single(l => l.Code == ChargeCodes.Freight).Amount);
        Assert.Equal(13.2m, quote.Lines.Single(l => l.Code == ChargeCodes.Fuel).Amount);
        Assert.Equal(QuoteCalculator.CustomsClearanceFee, quote.Lines.Single(l => l.Code == ChargeCodes.Customs).Amount);
        Assert.Equal(210.2m, quote.Total);
    }
}

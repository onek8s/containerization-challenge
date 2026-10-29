using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Integration.Edifact;
using Xunit;

namespace Proseware.ControlTower.Tests.Integration;

public class IftminBuilderTests
{
    private static readonly DateTimeOffset Pickup = new(2026, 10, 29, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Builds_booking_with_references_parties_temperature_and_packages()
    {
        var shipment = TestData.ReeferShipment(Pickup);
        var leg = shipment.Legs[0];
        var message = new IftminBuilder().Build(shipment, leg, leg.Carrier!, "1", Pickup.AddHours(-1));

        Assert.Equal($"{shipment.Number}-1", message.First("BGM")!.Get(1));
        Assert.Contains(message.All("RFF"), r => r.Get(0, 0) == "CU" && r.Get(0, 1) == shipment.Number);
        Assert.Contains(message.All("RFF"), r => r.Get(0, 0) == "BN" && r.Get(0, 1) == "NRDL0000030");
        Assert.Equal("3", message.First("TDT")!.Get(2));
        Assert.Equal("FITRE-NWF", message.All("LOC").First(l => l.Get(0) == "5").Get(1, 0));
        Assert.Equal("FIVAN", message.All("LOC").First(l => l.Get(0) == "8").Get(1, 0));
        Assert.Equal(new[] { "CEL", "2", "8" }, message.First("RNG")![1].Components);
        Assert.Equal(2, message.All("GID").Count());
        Assert.Single(message.All("HAN"));
        Assert.Equal(shipment.Packages.Select(p => p.Sscc), message.All("GIN").Select(g => g.Get(1)));
        Assert.Equal("1000.5", message.All("CNT").First(c => c.Get(0, 0) == "7").Get(0, 1));
    }

    [Fact]
    public void Dangerous_goods_are_described_per_package()
    {
        var shipment = TestData.ReeferShipment(Pickup);
        shipment.Packages[0].DangerousGoods = new DangerousGoods { UnNumber = "1263", ProperShippingName = "PAINT", HazardClass = "3", PackingGroup = "III", NetQuantity = 180, NetQuantityUnit = "l", TunnelCode = "D/E" };
        var message = new IftminBuilder().Build(shipment, shipment.Legs[0], shipment.Legs[0].Carrier!, "1", Pickup);

        var dgs = Assert.Single(message.All("DGS"));
        Assert.Equal("ADR", dgs.Get(0));
        Assert.Equal("3", dgs.Get(1));
        Assert.Equal("1263", dgs.Get(2));
        Assert.Contains(message.All("MEA"), m => m.Get(0) == "AAA" && m.Get(2, 0) == "LTR" && m.Get(2, 1) == "180");
    }

    [Fact]
    public void Written_interchange_parses_back()
    {
        var shipment = TestData.ReeferShipment(Pickup);
        var interchange = new EdifactInterchange { SenderId = "6430012345000", SenderQualifier = "14", RecipientId = "003712345678", RecipientQualifier = "30", Prepared = Pickup.UtcDateTime, ControlReference = "26102906000001" };
        interchange.Messages.Add(new IftminBuilder().Build(shipment, shipment.Legs[1], shipment.Legs[1].Carrier!, "1", Pickup, "5"));

        var parsed = EdifactReader.Parse(EdifactWriter.Write(interchange));

        Assert.Equal("5", parsed.Messages[0].First("BGM")!.Get(2));
        var consignor = parsed.Messages[0].All("NAD").First(n => n.Get(0) == "CZ");
        Assert.Equal("Northwind Foods Oy", consignor.Get(3));
        Assert.Equal("Tikkurilantie 140", consignor.Get(4));
    }
}

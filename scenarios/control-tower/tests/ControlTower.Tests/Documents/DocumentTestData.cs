using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Locations;
using Proseware.ControlTower.Domain.Parties;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Tests.Documents;

internal static class DocumentTestData
{
    public static Shipment ReeferShipmentWithDangerousGoods()
    {
        var origin = new Location { Id = 1, Code = "FITRE-NWF", Name = "Northwind Foods Tampere", Street = "Hatanpään valtatie 30", PostalCode = "33100", City = "Tampere", CountryCode = "FI", Position = GeoPoints.Create(61.487, 23.766) };
        var destination = new Location { Id = 2, Code = "SESTO-AWN", Name = "Adventure Works Nordic", Street = "Lagervägen 3", PostalCode = "126 34", City = "Hägersten", CountryCode = "SE", Position = GeoPoints.Create(59.298, 17.98), TimeZoneId = "W. Europe Standard Time" };
        var carrier = new Carrier { Id = 1, Code = "NRDL", Name = "Nordic Linehaul Oy", Mode = TransportMode.Road };
        var s = new Shipment
        {
            Id = 42,
            Number = ShipmentNumber.Format(2024, 4242),
            Customer = new Customer { Id = 1, Code = "NWF", Name = "Northwind Foods Oy" },
            CustomerReference = "PO-77^1",
            Mode = TransportMode.Road,
            IncotermCode = "DAP",
            Origin = origin,
            OriginId = 1,
            Destination = destination,
            DestinationId = 2,
            ShipperName = "Northwind Foods Oy",
            ConsigneeName = "Adventure Works Nordic AB",
            GoodsDescription = "Chilled ready meals",
            TemperatureMin = 2,
            TemperatureMax = 6,
            PlannedPickup = new DateTimeOffset(2024, 6, 10, 6, 0, 0, TimeSpan.Zero),
            PlannedDelivery = new DateTimeOffset(2024, 6, 12, 12, 0, 0, TimeSpan.Zero)
        };
        s.Packages.Add(new Package
        {
            Id = 1,
            Sscc = Sscc.Create('3', Sscc.DefaultCompanyPrefix, 424201),
            Kind = PackageKind.Pallet,
            WeightKg = 410.5m,
            LengthCm = 120,
            WidthCm = 80,
            HeightCm = 150,
            Stackable = false,
            DangerousGoods = new DangerousGoods { UnNumber = "1263", ProperShippingName = "PAINT", HazardClass = "3", PackingGroup = "III", NetQuantity = 120, NetQuantityUnit = "l" }
        });
        s.Packages.Add(new Package { Id = 2, Sscc = Sscc.Create('3', Sscc.DefaultCompanyPrefix, 424202), Kind = PackageKind.HalfPallet, WeightKg = 180, LengthCm = 80, WidthCm = 60, HeightCm = 100 });
        s.Legs.Add(new Leg { Id = 1, Sequence = 1, Mode = TransportMode.Road, Carrier = carrier, CarrierId = 1, From = origin, FromId = 1, To = destination, ToId = 2, CarrierReference = "NRDL0042420" });
        return s;
    }
}

using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Locations;
using Proseware.ControlTower.Domain.Parties;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Tests.Integration;

internal static class TestData
{
    public static Location Tampere => new() { Id = 1, Code = "FITRE-NWF", Name = "Northwind Foods Tampere", Kind = LocationKind.CustomerSite, Street = "Hatanpään valtatie 30", PostalCode = "33100", City = "Tampere", CountryCode = "FI", Position = GeoPoints.Create(61.4870, 23.7660), TimeZoneId = "FLE Standard Time" };

    public static Location Vantaa => new() { Id = 2, Code = "FIVAN", Name = "Proseware Terminal Vantaa", Kind = LocationKind.Terminal, Street = "Tikkurilantie 140", PostalCode = "01530", City = "Vantaa", CountryCode = "FI", Position = GeoPoints.Create(60.3133, 24.9637), TimeZoneId = "FLE Standard Time", OpensAt = new TimeSpan(5, 0, 0), ClosesAt = new TimeSpan(22, 0, 0) };

    public static Location Oulu => new() { Id = 3, Code = "FIOUL-FAB", Name = "Fabrikam Machinery Oulu", Kind = LocationKind.CustomerSite, Street = "Teknologiantie 4", PostalCode = "90590", City = "Oulu", CountryCode = "FI", Position = GeoPoints.Create(65.0580, 25.4430), TimeZoneId = "FLE Standard Time" };

    public static Carrier Nrdl => new() { Id = 1, Code = "NRDL", Name = "Nordic Linehaul Oy", Mode = TransportMode.Road, Integration = CarrierIntegration.EdifactSftp, EdiPartnerId = "003712345678", EdiQualifier = "30" };

    public static Shipment ReeferShipment(DateTimeOffset pickup)
    {
        var from = Tampere;
        var via = Vantaa;
        var to = Oulu;
        var carrier = Nrdl;
        var s = new Shipment
        {
            Id = 39,
            Number = ShipmentNumber.Format(2026, 3),
            CustomerId = 1,
            CustomerReference = "NWF-61204",
            Mode = TransportMode.Road,
            IncotermCode = "DAP",
            Status = ShipmentStatus.Booked,
            OriginId = from.Id, Origin = from,
            DestinationId = to.Id, Destination = to,
            ShipperName = "Northwind Foods Oy",
            ConsigneeName = "Fabrikam Machinery Oulu",
            PlannedPickup = pickup,
            PlannedDelivery = pickup.AddDays(1).AddHours(4),
            GoodsDescription = "Chilled dairy products",
            TemperatureMin = 2,
            TemperatureMax = 8
        };
        s.Packages.Add(new Package { Id = 1, Sscc = Sscc.Create('3', Sscc.DefaultCompanyPrefix, 300), Kind = PackageKind.Pallet, WeightKg = 512, LengthCm = 120, WidthCm = 80, HeightCm = 115 });
        s.Packages.Add(new Package { Id = 2, Sscc = Sscc.Create('3', Sscc.DefaultCompanyPrefix, 301), Kind = PackageKind.Pallet, WeightKg = 488.5m, LengthCm = 120, WidthCm = 80, HeightCm = 115, Stackable = false });
        s.Legs.Add(new Leg { Id = 10, Sequence = 1, Mode = TransportMode.Road, CarrierId = carrier.Id, Carrier = carrier, FromId = from.Id, From = from, ToId = via.Id, To = via, PlannedDeparture = pickup, PlannedArrival = pickup.AddHours(4), Status = LegStatus.Confirmed, CarrierReference = "NRDL0000030", VehicleId = 5 });
        s.Legs.Add(new Leg { Id = 11, Sequence = 2, Mode = TransportMode.Road, CarrierId = carrier.Id, Carrier = carrier, FromId = via.Id, From = via, ToId = to.Id, To = to, PlannedDeparture = pickup.AddHours(12), PlannedArrival = pickup.AddDays(1).AddHours(4), Status = LegStatus.Confirmed, CarrierReference = "NRDL0000031", VehicleId = 6 });
        return s;
    }
}

using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Fleet;
using Proseware.ControlTower.Domain.Locations;
using Proseware.ControlTower.Domain.Scanning;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Rules.Facts;

namespace Proseware.ControlTower.Rules;

/// <summary>Turns loaded entities into rule facts.</summary>
public static class AlertFactsBuilder
{
    private static readonly string[] FoodWords = { "food", "dairy", "bakery", "meals", "meat", "fish", "fruit", "vegetable", "wine" };

    public static AlertFacts Build(
        DateTimeOffset now,
        IEnumerable<Shipment> shipments,
        IEnumerable<Vehicle> vehicles,
        IReadOnlyDictionary<int, DateTimeOffset?> stoppedSince,
        IEnumerable<Location> locations,
        IEnumerable<ScanEvent> recentScans)
    {
        var facts = new AlertFacts { Now = now };
        var locationList = locations.ToList();
        var terminalCodes = locationList.ToDictionary(l => l.Id, l => l.Code);

        foreach (var s in shipments)
        {
            var leg = s.Legs.OrderBy(l => l.Sequence).FirstOrDefault(l => l.Status == LegStatus.Departed)
                      ?? (s.Status is ShipmentStatus.Booked or ShipmentStatus.PickedUp ? s.Legs.OrderBy(l => l.Sequence).FirstOrDefault() : null);
            var route = s.Legs.SelectMany(l => new[] { l.FromId, l.ToId }).Distinct().ToList();
            facts.Shipments.Add(new ShipmentFact
            {
                ShipmentId = s.Id,
                Number = s.Number,
                Status = s.Status,
                Mode = s.Mode,
                PlannedPickup = s.PlannedPickup,
                ActualPickup = s.ActualPickup,
                PlannedDelivery = s.PlannedDelivery,
                Eta = s.Eta,
                TemperatureMin = s.TemperatureMin,
                TemperatureMax = s.TemperatureMax,
                CrossesCustomsBorder = s.CrossesCustomsBorder,
                GrossWeightKg = s.GrossWeightKg,
                VehicleId = leg?.VehicleId,
                RouteLocationIds = route,
                IsFoodstuff = FoodWords.Any(w => s.GoodsDescription.Contains(w, StringComparison.OrdinalIgnoreCase))
            });
            foreach (var d in s.CustomsDeclarations)
            {
                facts.Customs.Add(new CustomsFact
                {
                    ShipmentId = s.Id,
                    DeclarationId = d.Id,
                    Type = d.Type,
                    Status = d.Status,
                    SubmittedAt = d.SubmittedAt,
                    RejectionReason = d.RejectionReason
                });
            }
            foreach (var p in s.Packages.Where(p => p.DangerousGoods is not null))
            {
                facts.DangerousGoods.Add(new DangerousGoodsFact
                {
                    ShipmentId = s.Id,
                    VehicleId = leg?.Status == LegStatus.Departed || s.Status == ShipmentStatus.PickedUp ? leg?.VehicleId : null,
                    Sscc = p.Sscc,
                    UnNumber = p.DangerousGoods!.UnNumber,
                    HazardClass = p.DangerousGoods.HazardClass,
                    LimitedQuantity = p.DangerousGoods.LimitedQuantity
                });
            }
        }

        foreach (var v in vehicles)
        {
            stoppedSince.TryGetValue(v.Id, out var stopped);
            facts.Vehicles.Add(new VehicleReadingFact
            {
                VehicleId = v.Id,
                Plate = v.Plate,
                LastSignalAt = v.LastPositionAt,
                Temperature = v.LastTemperature,
                SpeedKmh = v.LastSpeedKmh ?? 0,
                StoppedSince = stopped,
                AtKnownLocation = v.LastPosition is not null && locationList.Any(l => GeoPoints.IsInside(v.LastPosition, l)),
                AdrCertified = v.AdrCertified,
                MaxPayloadKg = v.MaxPayloadKg
            });
        }

        foreach (var scan in recentScans.Where(x => x.ShipmentId is not null))
        {
            facts.Scans.Add(new ScanFact
            {
                ShipmentId = scan.ShipmentId!.Value,
                Sscc = scan.Sscc,
                Kind = scan.Kind,
                TerminalId = scan.TerminalId,
                TerminalCode = terminalCodes.TryGetValue(scan.TerminalId, out var code) ? code : scan.TerminalId.ToString(),
                ScannedAt = scan.ScannedAt
            });
        }
        return facts;
    }
}

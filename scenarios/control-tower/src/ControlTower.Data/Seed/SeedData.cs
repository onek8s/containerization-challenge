using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Customs;
using Proseware.ControlTower.Domain.Fleet;
using Proseware.ControlTower.Domain.Locations;
using Proseware.ControlTower.Domain.Parties;
using Proseware.ControlTower.Domain.Pricing;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Domain.Tracking;

namespace Proseware.ControlTower.Data.Seed;

/// <summary>
/// Loads master data and a realistic set of shipments around "now" for test and training environments.
/// Deterministic for a given start time.
/// </summary>
public sealed class SeedData
{
    private readonly ControlTowerDbContext _db;
    private readonly DateTimeOffset _now;
    private readonly Random _rnd = new(20240611);

    public SeedData(ControlTowerDbContext db, DateTimeOffset now)
    {
        _db = db;
        _now = new DateTimeOffset(now.UtcDateTime.Date.AddHours(now.UtcDateTime.Hour), TimeSpan.Zero);
    }

    public int ShipmentCount { get; init; } = 180;

    public async Task RunAsync(CancellationToken ct = default)
    {
        if (await _db.Customers.AnyAsync(ct)) return;

        var customers = Insert(ReferenceData.Customers(_now));
        var carriers = Insert(ReferenceData.Carriers());
        await _db.SaveChangesAsync(ct);
        var customerById = customers.ToDictionary(x => x.OriginalId, x => x.Entity);
        var carrierById = carriers.ToDictionary(x => x.OriginalId, x => x.Entity);

        var locations = ReferenceData.Locations().ToList();
        foreach (var l in locations)
            if (l.CustomerId is { } c) l.CustomerId = customerById[c].Id;
        _db.Locations.AddRange(locations);

        var vehicles = ReferenceData.Vehicles().ToList();
        foreach (var v in vehicles)
        {
            v.Id = 0;
            if (v.CarrierId is { } c) v.CarrierId = carrierById[c].Id;
        }
        _db.Vehicles.AddRange(vehicles);

        var rateCards = ReferenceData.RateCards(_now.UtcDateTime.Date).ToList();
        foreach (var r in rateCards)
        {
            r.Id = 0;
            foreach (var b in r.Breaks) { b.Id = 0; b.RateCardId = 0; }
            if (r.CarrierId is { } c) r.CarrierId = carrierById[c].Id;
        }
        _db.RateCards.AddRange(rateCards);
        await _db.SaveChangesAsync(ct);

        var byCode = locations.ToDictionary(l => l.Code);
        var lanes = Lanes(byCode, customerById, carrierById);
        var sequence = 1;
        for (var i = 0; i < ShipmentCount; i++)
        {
            var lane = lanes[_rnd.Next(lanes.Count)];
            var shipment = CreateShipment(lane, sequence++, vehicles);
            _db.Shipments.Add(shipment);
            if (i % 40 == 39) await _db.SaveChangesAsync(ct);
        }
        await _db.SaveChangesAsync(ct);

        await CreateAlertsAsync(ct);
    }

    private List<(int OriginalId, T Entity)> Insert<T>(IEnumerable<T> items) where T : class
    {
        var list = new List<(int, T)>();
        foreach (var item in items)
        {
            var idProp = typeof(T).GetProperty("Id")!;
            var original = (int)idProp.GetValue(item)!;
            idProp.SetValue(item, 0);
            _db.Add(item);
            list.Add((original, item));
        }
        return list;
    }

    private sealed record Lane(Customer Customer, Location Origin, Location Destination, TransportMode Mode, Carrier[] Carriers, Location[] Via, string Incoterm, string Goods, bool Reefer, bool Dg);

    private static List<Lane> Lanes(Dictionary<string, Location> l, Dictionary<int, Customer> c, Dictionary<int, Carrier> k) => new()
    {
        new(c[1], l["FITRE-NWF"], l["FIOUL-FAB"], TransportMode.Road, new[] { k[1] }, new[] { l["FIVAN"] }, "DAP", "Chilled dairy products", true, false),
        new(c[1], l["FITRE-NWF"], l["SESTO-AWN"], TransportMode.Road, new[] { k[1], k[4] }, new[] { l["FITKU"] }, "DAP", "Frozen bakery products", true, false),
        new(c[1], l["FITRE-NWF"], l["EETLL"], TransportMode.Road, new[] { k[2] }, new[] { l["FIVAN"] }, "FCA", "Chilled ready meals", true, false),
        new(c[2], l["FIJYV-CPH"], l["DEMUC-CWG"], TransportMode.Road, new[] { k[1], k[2] }, new[] { l["FIVAN"], l["EETLL"] }, "CIP", "Pharmaceutical products, 2-8 °C", true, false),
        new(c[2], l["FIJYV-CPH"], l["CNPVG-AP"], TransportMode.Air, new[] { k[1], k[7] }, new[] { l["FIHEL-AP"] }, "CIP", "Medical reagents", true, true),
        new(c[3], l["FIOUL-FAB"], l["USNYC"], TransportMode.Sea, new[] { k[1], k[6] }, new[] { l["FIVUO"] }, "FOB", "Industrial gearboxes", false, false),
        new(c[3], l["FIOUL-FAB"], l["PLPOZ-TRR"], TransportMode.Road, new[] { k[1], k[2] }, new[] { l["FIVAN"] }, "DAP", "Machine spare parts", false, false),
        new(c[3], l["FIOUL-FAB"], l["GBBHM-LUC"], TransportMode.Road, new[] { k[1] }, new[] { l["FIVAN"] }, "DDP", "Hydraulic units", false, true),
        new(c[4], l["SESTO-AWN"], l["FIESP-WWI"], TransportMode.Road, new[] { k[4], k[3] }, new[] { l["FITKU"] }, "DAP", "Outdoor equipment", false, false),
        new(c[5], l["CNSZX-SUP"], l["FIESP-WWI"], TransportMode.Sea, new[] { k[5], k[3] }, new[] { l["CNSHA"], l["FIVUO"] }, "FOB", "Household electronics", false, false),
        new(c[5], l["CNSZX-SUP"], l["FIESP-WWI"], TransportMode.Air, new[] { k[7], k[8] }, new[] { l["CNPVG-AP"], l["FIHEL-AP"] }, "EXW", "Mobile phone accessories", false, true),
        new(c[5], l["NLRTM"], l["FIESP-WWI"], TransportMode.Sea, new[] { k[6], k[3] }, new[] { l["FIVUO"] }, "CFR", "Garden furniture", false, false),
        new(c[6], l["SEMMA-TST"], l["FIVAN"], TransportMode.Road, new[] { k[4] }, new[] { l["SEGOT"] }, "DAP", "Toys and games", false, false),
        new(c[7], l["FILAH-WGP"], l["SESTO-AWN"], TransportMode.Road, new[] { k[1], k[4] }, new[] { l["FITKU"] }, "DAP", "Paints, flammable", false, true),
        new(c[7], l["FILAH-WGP"], l["FITRE-NWF"], TransportMode.Road, new[] { k[3] }, Array.Empty<Location>(), "DAP", "Paints and lacquers", false, true),
        new(c[8], l["FIVAN"], l["NOOSL-ASH"], TransportMode.Road, new[] { k[4] }, new[] { l["SEGOT"], l["NOSVB"] }, "DDP", "Skis and ski boots", false, false),
        new(c[9], l["DEMUC-CWG"], l["FIVAN"], TransportMode.Road, new[] { k[2] }, new[] { l["EETLL"] }, "DAP", "Wine, 75 cl bottles", false, false),
        new(c[9], l["DEHAM"], l["FIVUO"], TransportMode.Sea, new[] { k[6] }, Array.Empty<Location>(), "CIF", "Wine, pallets", false, false),
        new(c[10], l["FIVAN"], l["GBBHM-LUC"], TransportMode.Road, new[] { k[1] }, Array.Empty<Location>(), "DAP", "Printed books", false, false),
        new(c[11], l["PLPOZ-TRR"], l["FIVAN"], TransportMode.Road, new[] { k[2] }, new[] { l["EETLL"] }, "FCA", "Laboratory equipment", false, false),
        new(c[3], l["FIOUL-FAB"], l["FIESP-WWI"], TransportMode.Road, new[] { k[8] }, Array.Empty<Location>(), "DAP", "Spare parts parcel", false, false),
        new(c[5], l["FIESP-WWI"], l["FITRE-NWF"], TransportMode.Road, new[] { k[8] }, Array.Empty<Location>(), "DAP", "Consumer electronics parcel", false, false),
    };

    private Shipment CreateShipment(Lane lane, int sequence, List<Vehicle> vehicles)
    {
        var transitDays = lane.Mode switch { TransportMode.Sea => 18 + _rnd.Next(25), TransportMode.Air => 2 + _rnd.Next(3), _ => 1 + lane.Via.Length + _rnd.Next(2) };
        var pickup = _now.AddDays(_rnd.Next(-35, 8)).AddHours(_rnd.Next(-6, 6));
        pickup = new DateTimeOffset(pickup.UtcDateTime.Date.AddHours(6 + _rnd.Next(8)), TimeSpan.Zero);
        var delivery = pickup.AddDays(transitDays).Date.AddHours(12);

        var s = new Shipment
        {
            Number = ShipmentNumber.Format(pickup.Year, sequence),
            CustomerId = lane.Customer.Id,
            CustomerReference = $"{lane.Customer.Code}-{40000 + _rnd.Next(60000)}",
            Mode = lane.Mode,
            IncotermCode = lane.Incoterm,
            OriginId = lane.Origin.Id,
            Origin = lane.Origin,
            DestinationId = lane.Destination.Id,
            Destination = lane.Destination,
            ShipperName = lane.Origin.CustomerId == lane.Customer.Id ? lane.Customer.Name : lane.Origin.Name,
            ConsigneeName = lane.Destination.Name,
            ConsigneeEmail = "goods.in@" + lane.Destination.City.ToLowerInvariant().Replace("ä", "a").Replace("ö", "o").Replace("ü", "u").Replace(" ", "") + ".example",
            PlannedPickup = pickup,
            PlannedDelivery = new DateTimeOffset(delivery, TimeSpan.Zero),
            GoodsDescription = lane.Goods,
            DeclaredValue = 1500 + _rnd.Next(80) * 500,
            TemperatureMin = lane.Reefer ? 2 : null,
            TemperatureMax = lane.Reefer ? 8 : null,
            CreatedBy = "seed",
            CreatedAt = pickup.AddDays(-_rnd.Next(1, 6)),
            Status = ShipmentStatus.Booked
        };
        s.UpdatedAt = s.CreatedAt;

        var packageCount = lane.Mode == TransportMode.Sea && _rnd.Next(3) == 0 ? 1 : 1 + _rnd.Next(lane.Carriers.Any(c => c.Code == "PRCL") ? 3 : 8);
        for (var p = 0; p < packageCount; p++)
        {
            var container = lane.Mode == TransportMode.Sea && packageCount == 1;
            var parcel = lane.Carriers.Any(c => c.Code == "PRCL");
            var pkg = new Package
            {
                Sscc = Domain.Shipments.Sscc.Create('3', Domain.Shipments.Sscc.DefaultCompanyPrefix, sequence * 100L + p),
                Kind = container ? PackageKind.Container40HighCube : parcel ? PackageKind.Parcel : (_rnd.Next(4) == 0 ? PackageKind.HalfPallet : PackageKind.Pallet),
                Quantity = 1,
                WeightKg = container ? 9000 + _rnd.Next(12000) : parcel ? 2 + _rnd.Next(25) : 120 + _rnd.Next(700),
                LengthCm = container ? 1203 : parcel ? 40 + _rnd.Next(30) : 120,
                WidthCm = container ? 235 : parcel ? 30 + _rnd.Next(20) : (_rnd.Next(4) == 0 ? 60 : 80),
                HeightCm = container ? 269 : parcel ? 20 + _rnd.Next(30) : 80 + _rnd.Next(110),
                Stackable = _rnd.Next(3) != 0,
                ContainerNumber = container ? ContainerNumberFor(sequence) : null,
                SealNumber = container ? $"SL{700000 + sequence}" : null
            };
            if (lane.Dg && p == 0)
            {
                pkg.DangerousGoods = lane.Goods.Contains("Paint", StringComparison.OrdinalIgnoreCase)
                    ? new DangerousGoods { UnNumber = "1263", ProperShippingName = "PAINT", HazardClass = "3", PackingGroup = "III", NetQuantity = 180, NetQuantityUnit = "l", TunnelCode = "D/E" }
                    : lane.Goods.Contains("Hydraulic", StringComparison.OrdinalIgnoreCase)
                        ? new DangerousGoods { UnNumber = "3082", ProperShippingName = "ENVIRONMENTALLY HAZARDOUS SUBSTANCE, LIQUID, N.O.S.", HazardClass = "9", PackingGroup = "III", NetQuantity = 60, LimitedQuantity = false, TunnelCode = "-" }
                        : lane.Mode == TransportMode.Air && lane.Customer.Code == "CPH"
                            ? new DangerousGoods { UnNumber = "1845", ProperShippingName = "DRY ICE", HazardClass = "9", PackingGroup = "III", NetQuantity = 25 }
                            : new DangerousGoods { UnNumber = "3481", ProperShippingName = "LITHIUM ION BATTERIES PACKED WITH EQUIPMENT", HazardClass = "9", NetQuantity = 4, LimitedQuantity = true };
            }
            s.Packages.Add(pkg);
        }

        // Legs: origin -> via... -> destination, carriers assigned in order (last carrier repeats).
        var stops = new List<Location> { lane.Origin };
        stops.AddRange(lane.Via);
        stops.Add(lane.Destination);
        var legCount = stops.Count - 1;
        var legDuration = TimeSpan.FromTicks((s.PlannedDelivery - s.PlannedPickup).Ticks / legCount);
        for (var i = 0; i < legCount; i++)
        {
            var carrier = lane.Carriers[Math.Min(i, lane.Carriers.Length - 1)];
            var mode = carrier.Mode == TransportMode.Road && lane.Mode != TransportMode.Road && i > 0 && i < legCount - 1 ? lane.Mode : carrier.Mode;
            var dep = s.PlannedPickup + legDuration * i;
            s.Legs.Add(new Leg
            {
                Sequence = i + 1,
                Mode = mode,
                CarrierId = carrier.Id,
                Carrier = carrier,
                FromId = stops[i].Id,
                From = stops[i],
                ToId = stops[i + 1].Id,
                To = stops[i + 1],
                PlannedDeparture = dep,
                PlannedArrival = dep + legDuration,
                Status = LegStatus.Confirmed,
                CarrierReference = CarrierReferenceFor(carrier, sequence, i),
                VesselOrFlight = mode == TransportMode.Sea ? $"OCEANIX {new[] { "AURORA", "BOREAS", "CALYPSO", "DELPHI" }[sequence % 4]} {420 + sequence % 7}W" : mode == TransportMode.Air ? $"AF{3100 + sequence % 40}" : null,
                VehicleId = mode == TransportMode.Road && carrier.Code is not "PRCL" ? vehicles[_rnd.Next(vehicles.Count)].Id : null,
                DistanceKm = Math.Round(GeoPoints.DistanceMeters(stops[i].Position, stops[i + 1].Position) / 1000 * 1.25, 0)
            });
        }

        // Charges from the cheapest matching rate.
        var quote = new QuoteCalculator().Calculate(
            new QuoteRequest(lane.Mode, lane.Origin.CountryCode, lane.Origin.PostalCode, lane.Destination.CountryCode, s.Packages, pickup.UtcDateTime.Date, s.CrossesCustomsBorder, s.DeclaredValue, lane.Customer.DiscountPercent),
            _db.RateCards.Local, lane.Reefer).FirstOrDefault();
        if (quote is not null)
            foreach (var line in quote.Lines)
                s.Charges.Add(new Charge { Code = line.Code, Description = line.Description, Amount = line.Amount, Currency = quote.Currency });

        PlayMilestones(s);
        if (s.CrossesCustomsBorder) AddDeclaration(s, lane);
        return s;
    }

    private void PlayMilestones(Shipment s)
    {
        void Add(string code, DateTimeOffset at, Location? where, string source)
        {
            if (at > _now) return;
            ShipmentStateMachine.Apply(s, new Milestone { Code = code, OccurredAt = at, RecordedAt = at.AddMinutes(_rnd.Next(1, 20)), LocationId = where?.Id, LocationText = where?.City, Source = source });
        }

        Add(MilestoneCodes.Booked, s.CreatedAt, null, s.CreatedBy);
        var drift = TimeSpan.FromMinutes(_rnd.Next(-90, 240));
        // Roughly one in eight shipments runs into a longer delay somewhere.
        var bigDelay = _rnd.Next(8) == 0 ? TimeSpan.FromHours(6 + _rnd.Next(30)) : TimeSpan.Zero;
        var cancelled = _rnd.Next(40) == 0 && s.PlannedPickup > _now.AddDays(-2);
        if (cancelled)
        {
            s.CancelReason = "Customer cancelled the order";
            Add(MilestoneCodes.Cancelled, s.CreatedAt.AddHours(5), null, "customer");
            foreach (var l in s.Legs) l.Status = LegStatus.Cancelled;
            return;
        }

        foreach (var leg in s.Legs.OrderBy(l => l.Sequence))
        {
            var source = leg.Carrier?.Integration switch
            {
                CarrierIntegration.EdifactSftp => "edi:" + leg.Carrier.EdiPartnerId,
                CarrierIntegration.RestApi => "api:" + leg.Carrier.Code,
                _ => "ops"
            };
            var departed = leg.PlannedDeparture + drift;
            var arrived = leg.PlannedArrival + drift + (leg.Sequence == s.Legs.Count ? bigDelay : TimeSpan.Zero);
            if (departed <= _now)
            {
                leg.ActualDeparture = departed;
                leg.Status = LegStatus.Departed;
                Add(leg.Sequence == 1 ? MilestoneCodes.PickedUp : MilestoneCodes.Departed, departed, leg.From, source);
            }
            if (leg.Sequence == s.Legs.Count && arrived.AddHours(-4) <= _now && arrived > _now)
                Add(MilestoneCodes.OutForDelivery, arrived.AddHours(-4), leg.From, source);
            if (arrived <= _now)
            {
                leg.ActualArrival = arrived;
                leg.Status = LegStatus.Arrived;
                if (leg.Sequence == s.Legs.Count)
                {
                    Add(MilestoneCodes.OutForDelivery, arrived.AddHours(-4), leg.From, source);
                    Add(MilestoneCodes.Delivered, arrived, leg.To, source);
                    s.PodSignedBy = new[] { "M. Virtanen", "A. Korhonen", "J. Nieminen", "S. Andersson", "K. Berg", "P. Nowak" }[_rnd.Next(6)];
                }
                else
                {
                    Add(leg.To!.Kind == LocationKind.Terminal ? MilestoneCodes.ArrivedAtTerminal : MilestoneCodes.Arrived, arrived, leg.To, source);
                }
            }
            else if (leg.Status == LegStatus.Departed)
            {
                leg.Eta = arrived;
            }
        }
        s.Eta = s.Legs.OrderBy(l => l.Sequence).Last().Eta ?? (s.Status == ShipmentStatus.Delivered ? null : s.PlannedDelivery + drift + bigDelay);
        s.EtaCalculatedAt = s.Eta is null ? null : _now;
    }

    private void AddDeclaration(Shipment s, Lane lane)
    {
        var import = lane.Destination.IsEuCustomsTerritory;
        var d = new CustomsDeclaration
        {
            Type = import ? DeclarationType.Import : DeclarationType.Export,
            Lrn = DeclarationValidator.NewLrn(s.Number.GetHashCode() & 0xFFFFFF, s.CreatedAt, 1),
            DeclarantEori = "FI0123456-7".Replace("-", ""),
            ConsignorEori = import ? null : lane.Customer.Eori,
            ConsigneeEori = import ? lane.Customer.Eori : null,
            OfficeOfDeclaration = import ? "FI001000" : "FI004400",
            OfficeOfExit = import ? null : (lane.Mode == TransportMode.Sea ? "FI001400" : lane.Mode == TransportMode.Air ? "FI003100" : "SE600001"),
            CreatedAt = s.CreatedAt.AddHours(2),
            Status = DeclarationStatus.Draft
        };
        d.Lines.Add(new DeclarationLine
        {
            LineNumber = 1,
            CommodityCode = lane.Goods switch
            {
                var g when g.Contains("electronics") || g.Contains("phone") => "85176200",
                var g when g.Contains("furniture") => "94017900",
                var g when g.Contains("Ski") => "95061100",
                var g when g.Contains("gearbox") || g.Contains("Hydraulic") => "84834023",
                var g when g.Contains("Medical") || g.Contains("Pharma") => "30049000",
                var g when g.Contains("book") => "49019900",
                _ => "84879090"
            },
            Description = lane.Goods,
            OriginCountry = lane.Origin.CountryCode,
            NetMassKg = Math.Round(s.GrossWeightKg * 0.92m, 3),
            GrossMassKg = s.GrossWeightKg,
            StatisticalValue = s.DeclaredValue ?? 1000,
            ProcedureCode = import ? "4000" : "1000",
            Packages = s.PackageCount,
            PackageKindCode = s.Packages[0].Kind == PackageKind.Parcel ? "PK" : s.Packages[0].Kind >= PackageKind.Container20 ? "CN" : "PX"
        });
        d.Lrn = $"PF{s.CreatedAt:yyMMdd}{s.Number[5..]}01";
        if (s.Status >= ShipmentStatus.PickedUp && s.Status != ShipmentStatus.Cancelled)
        {
            d.Status = DeclarationStatus.Submitted;
            d.SubmittedAt = s.ActualPickup?.AddHours(-3) ?? s.CreatedAt.AddHours(3);
            d.SubmitAttempts = 1;
            if (_rnd.Next(10) > 0)
            {
                d.Status = DeclarationStatus.Released;
                d.Mrn = $"{s.CreatedAt:yy}FI{_rnd.NextInt64(10000000000, 99999999999)}{(char)('A' + _rnd.Next(26))}{_rnd.Next(10, 99)}"[..18];
                d.AcceptedAt = d.SubmittedAt.Value.AddMinutes(4);
                d.ReleasedAt = d.SubmittedAt.Value.AddMinutes(20 + _rnd.Next(300));
            }
            else
            {
                d.Status = DeclarationStatus.UnderControl;
                d.Mrn = $"{s.CreatedAt:yy}FI{_rnd.NextInt64(10000000000, 99999999999)}C{_rnd.Next(10, 99)}"[..18];
                d.AcceptedAt = d.SubmittedAt.Value.AddMinutes(5);
            }
        }
        s.CustomsDeclarations.Add(d);
    }

    private async Task CreateAlertsAsync(CancellationToken ct)
    {
        var late = await _db.Shipments.Include(s => s.Legs)
            .Where(s => s.Status != ShipmentStatus.Delivered && s.Status != ShipmentStatus.Cancelled && s.Status != ShipmentStatus.Requested)
            .ToListAsync(ct);
        foreach (var s in late.Where(s => s.Eta > s.PlannedDelivery.AddHours(4)))
        {
            _db.Alerts.Add(new ShipmentAlert
            {
                ShipmentId = s.Id,
                RuleCode = AlertRules.EtaSlipped,
                Severity = s.Eta > s.PlannedDelivery.AddDays(1) ? Severity.Critical : Severity.Warning,
                Message = $"Estimated arrival {s.Eta:yyyy-MM-dd HH:mm} UTC is {(s.Eta!.Value - s.PlannedDelivery).TotalHours:0} h after the promised delivery.",
                RaisedAt = _now.AddHours(-_rnd.Next(1, 30)),
                LastSeenAt = _now
            });
        }
        foreach (var s in late.Where(s => s.Status == ShipmentStatus.Booked && s.PlannedPickup < _now.AddHours(-2)))
        {
            _db.Alerts.Add(new ShipmentAlert
            {
                ShipmentId = s.Id,
                RuleCode = AlertRules.PickupMissed,
                Severity = Severity.Warning,
                Message = $"Pickup planned at {s.PlannedPickup:yyyy-MM-dd HH:mm} UTC has not been confirmed.",
                RaisedAt = s.PlannedPickup.AddHours(2),
                LastSeenAt = _now
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    private static string CarrierReferenceFor(Carrier c, int sequence, int leg) => c.Code switch
    {
        "OCNX" => $"OCNX{2400000 + sequence * 3 + leg}",
        "NSFL" => $"NSFL{510000 + sequence}",
        "AFX" => $"615-{(10000000 + sequence * 7):D8}",
        "PRCL" => $"JJFI{6430000000L + sequence * 13}",
        "BALX" => $"BX{900000 + sequence}",
        _ => $"{c.Code}{sequence:D6}{leg}"
    };

    /// <summary>ISO 6346 container number with check digit.</summary>
    internal static string ContainerNumberFor(int sequence)
    {
        var body = $"OCXU{(100000 + sequence) % 1000000:D6}";
        const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            var ch = body[i];
            int value;
            if (char.IsDigit(ch)) value = ch - '0';
            else
            {
                // A=10, then skip multiples of 11 (B=12 ... Z=38)
                value = 10;
                for (var k = 0; k < letters.IndexOf(ch); k++)
                {
                    value++;
                    if (value % 11 == 0) value++;
                }
            }
            sum += value * (1 << i);
        }
        return body + (sum % 11 % 10);
    }
}

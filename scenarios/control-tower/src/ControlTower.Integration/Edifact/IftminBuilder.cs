using System.Globalization;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Parties;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Integration.Edifact;

/// <summary>Builds IFTMIN D.96A instruction messages (transport booking) for one leg of a shipment.</summary>
public sealed class IftminBuilder
{
    public const string MessageType = "IFTMIN";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public string OurPartyId { get; init; } = "6430012345000";
    public string OurPartyQualifier { get; init; } = "14";
    public string OurName { get; init; } = "Proseware Freight Oy";

    /// <summary>
    /// Message function: 9 = original, 5 = replace, 1 = cancellation.
    /// </summary>
    public EdifactMessage Build(Shipment shipment, Leg leg, Carrier carrier, string messageReference, DateTimeOffset now, string function = "9")
    {
        var from = leg.From ?? throw new ArgumentException("Leg origin is not loaded.", nameof(leg));
        var to = leg.To ?? throw new ArgumentException("Leg destination is not loaded.", nameof(leg));
        var m = new EdifactMessage { ReferenceNumber = messageReference, Type = MessageType, Version = "D", Release = "96A", Agency = "UN" };
        var s = m.Segments;

        s.Add(EdifactSegment.Create("BGM", "610", $"{shipment.Number}-{leg.Sequence}", function));
        s.Add(EdifactSegment.Composite("DTM", "137", now.UtcDateTime.ToString("yyyyMMddHHmm", Inv), "203"));
        s.Add(EdifactSegment.Create("TSR", "30", new[] { shipment.Mode == TransportMode.Air ? "3" : "2" }));
        s.Add(EdifactSegment.Composite("CNT", "7", Number(shipment.GrossWeightKg), "KGM"));
        s.Add(EdifactSegment.Composite("CNT", "11", shipment.PackageCount.ToString(Inv)));
        if (shipment.VolumeM3 > 0) s.Add(EdifactSegment.Composite("CNT", "15", Number(shipment.VolumeM3), "MTQ"));
        if (!string.IsNullOrEmpty(shipment.GoodsDescription))
            s.Add(EdifactSegment.Create("FTX", "AAA", null, null, Split(shipment.GoodsDescription, 70, 5)));
        s.Add(EdifactSegment.Composite("RFF", "CU", shipment.Number));
        if (!string.IsNullOrEmpty(shipment.CustomerReference))
            s.Add(EdifactSegment.Composite("RFF", "CR", shipment.CustomerReference));
        if (!string.IsNullOrEmpty(leg.CarrierReference))
            s.Add(EdifactSegment.Composite("RFF", "BN", leg.CarrierReference));
        s.Add(EdifactSegment.Create("TDT", "20", null, ModeCode(leg.Mode), null, new[] { carrier.Code, "172", "", carrier.Name }));
        s.Add(EdifactSegment.Create("TOD", "6", null, new[] { shipment.IncotermCode }));

        s.Add(EdifactSegment.Create("LOC", "5", new[] { from.Code, "", "", from.Name }));
        s.Add(EdifactSegment.Composite("DTM", "234", leg.PlannedDeparture.UtcDateTime.ToString("yyyyMMddHHmm", Inv), "203"));
        s.Add(EdifactSegment.Create("LOC", "8", new[] { to.Code, "", "", to.Name }));
        s.Add(EdifactSegment.Composite("DTM", "2", leg.PlannedArrival.UtcDateTime.ToString("yyyyMMddHHmm", Inv), "203"));

        s.Add(EdifactSegment.Create("NAD", "CZ", null, null, Split(shipment.ShipperName, 35, 2), new[] { from.Street }, from.City, null, from.PostalCode, from.CountryCode));
        s.Add(EdifactSegment.Create("NAD", "CN", null, null, Split(shipment.ConsigneeName, 35, 2), new[] { to.Street }, to.City, null, to.PostalCode, to.CountryCode));
        if (!string.IsNullOrEmpty(shipment.NotifyPhone))
            s.Add(EdifactSegment.Create("CTA", "IC", null).Add(shipment.NotifyPhone, "TE"));
        s.Add(EdifactSegment.Create("NAD", "FW", new[] { OurPartyId, "", OurPartyQualifier }, null, new[] { OurName }));
        s.Add(EdifactSegment.Create("NAD", "CA", new[] { carrier.EdiPartnerId ?? carrier.Code, "", carrier.EdiQualifier ?? "ZZZ" }));

        if (shipment.IsReefer)
        {
            s.Add(EdifactSegment.Create("TMP", "2", new[] { Number(shipment.TemperatureMin ?? shipment.TemperatureMax ?? 0), "CEL" }));
            s.Add(EdifactSegment.Create("RNG", "5", new[] { "CEL", Number(shipment.TemperatureMin ?? -99), Number(shipment.TemperatureMax ?? 99) }));
        }

        var gid = 1;
        foreach (var p in shipment.Packages)
        {
            s.Add(EdifactSegment.Create("GID", gid++, new[] { p.Quantity.ToString(Inv), PackageCode(p.Kind) }));
            s.Add(EdifactSegment.Create("MEA", "AAE", "G", new[] { "KGM", Number(p.WeightKg * p.Quantity) }));
            if (p.LengthCm > 0)
                s.Add(EdifactSegment.Create("DIM", "1", new[] { "CMT", p.LengthCm.ToString(Inv), p.WidthCm.ToString(Inv), p.HeightCm.ToString(Inv) }));
            if (!p.Stackable) s.Add(EdifactSegment.Composite("HAN", "NST"));
            if (!string.IsNullOrEmpty(p.Marks)) s.Add(EdifactSegment.Create("PCI", "24", new[] { p.Marks }));
            s.Add(EdifactSegment.Create("GIN", "BJ", p.Sscc));
            if (p.ContainerNumber is not null)
                s.Add(EdifactSegment.Create("SGP", p.ContainerNumber, p.Quantity));
            if (p.DangerousGoods is { } dg)
            {
                var regime = leg.Mode switch { TransportMode.Sea => "IMD", TransportMode.Air => "IAT", TransportMode.Rail => "RID", _ => "ADR" };
                s.Add(EdifactSegment.Create("DGS", regime, new[] { dg.HazardClass }, dg.UnNumber, null, dg.PackingGroup, null, null, null, null, null, null, dg.TunnelCode));
                s.Add(EdifactSegment.Create("FTX", "AAD", null, null, Split(dg.ProperShippingName, 70, 3)));
                s.Add(EdifactSegment.Create("MEA", "AAA", "AAF", new[] { dg.NetQuantityUnit == "l" ? "LTR" : "KGM", Number(dg.NetQuantity) }));
                if (dg.LimitedQuantity) s.Add(EdifactSegment.Create("FTX", "AAC", null, null, new[] { "LIMITED QUANTITY" }));
            }
        }

        if (shipment.CustomsDeclarations.FirstOrDefault(d => d.Mrn is not null) is { } decl)
            s.Add(EdifactSegment.Composite("RFF", "ABT", decl.Mrn!));

        return m;
    }

    public static string ModeCode(TransportMode mode) => mode switch
    {
        TransportMode.Sea => "1",
        TransportMode.Rail => "2",
        TransportMode.Road => "3",
        TransportMode.Air => "4",
        _ => "3"
    };

    public static string PackageCode(PackageKind kind) => kind switch
    {
        PackageKind.Parcel => "PK",
        PackageKind.Pallet => "PX",
        PackageKind.HalfPallet => "PX",
        PackageKind.Crate => "CR",
        PackageKind.Container20 or PackageKind.Container40 or PackageKind.Container40HighCube => "CN",
        _ => "PK"
    };

    private static string Number(decimal value) => value.ToString("0.###", Inv);

    internal static string[] Split(string text, int length, int maxParts)
    {
        var parts = new List<string>();
        var rest = text.Trim();
        while (rest.Length > 0 && parts.Count < maxParts)
        {
            var take = Math.Min(length, rest.Length);
            parts.Add(rest[..take]);
            rest = rest[take..].TrimStart();
        }
        return parts.Count == 0 ? new[] { "" } : parts.ToArray();
    }
}

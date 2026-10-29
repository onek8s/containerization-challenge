using Proseware.ControlTower.Domain.Documents;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Documents;

public sealed class ShipmentDocumentModel
{
    public ShipmentDocumentModel(Shipment shipment, DocumentKind kind, IssuerOptions issuer, DateTimeOffset issuedAt, string trackingUrl)
    {
        Shipment = shipment;
        Kind = kind;
        Issuer = issuer;
        IssuedAt = issuedAt;
        TrackingUrl = trackingUrl;
        QrCodeDataUri = QrCodes.PngDataUri(trackingUrl);
    }

    public Shipment Shipment { get; }
    public DocumentKind Kind { get; }
    public IssuerOptions Issuer { get; }
    public DateTimeOffset IssuedAt { get; }
    public string TrackingUrl { get; }
    public string QrCodeDataUri { get; }

    public string Title => Kind switch
    {
        DocumentKind.Cmr => "CMR consignment note",
        DocumentKind.HouseBillOfLading => "House bill of lading",
        DocumentKind.HouseAirWaybill => "House air waybill",
        DocumentKind.DeliveryNote => "Delivery note",
        DocumentKind.DangerousGoodsNote => "Dangerous goods note",
        _ => Kind.ToString()
    };

    public string DocumentNumber => Kind switch
    {
        DocumentKind.Cmr => $"CMR-{Shipment.Number}",
        DocumentKind.HouseBillOfLading => $"PFHB{Shipment.Number[2..].Replace("-", "")}",
        DocumentKind.HouseAirWaybill => $"PFH{Shipment.Number[2..].Replace("-", "")}",
        DocumentKind.DeliveryNote => $"DN-{Shipment.Number}",
        DocumentKind.DangerousGoodsNote => $"DGN-{Shipment.Number}",
        _ => Shipment.Number
    };

    public Leg? MainLeg => Shipment.Legs
        .OrderByDescending(l => l.Mode == Shipment.Mode)
        .ThenByDescending(l => l.DistanceKm ?? 0)
        .FirstOrDefault();

    public string Local(DateTimeOffset? utc, string? timeZoneId)
    {
        if (utc is null) return "";
        if (string.IsNullOrEmpty(timeZoneId)) return utc.Value.ToString("yyyy-MM-dd HH:mm 'UTC'");
        try
        {
            return Domain.LocalTime.ToLocal(utc.Value, timeZoneId).ToString("yyyy-MM-dd HH:mm");
        }
        catch (TimeZoneNotFoundException)
        {
            return utc.Value.ToString("yyyy-MM-dd HH:mm 'UTC'");
        }
    }
}

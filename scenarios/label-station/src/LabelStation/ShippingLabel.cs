namespace LabelStation;

public sealed record ShippingLabel(
    string OrderNumber,
    string Carrier,
    string ShipToName,
    string ShipToAddress,
    int ParcelIndex,
    int ParcelCount,
    decimal WeightKg)
{
    public string TrackingCode => $"{OrderNumber}-{ParcelIndex:D2}";
}

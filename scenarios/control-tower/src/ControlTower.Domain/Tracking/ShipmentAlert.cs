namespace Proseware.ControlTower.Domain.Tracking;

/// <summary>An exception raised by the rules for a shipment, handled by the operations desk.</summary>
public class ShipmentAlert
{
    public long Id { get; set; }
    public int ShipmentId { get; set; }
    public string RuleCode { get; set; } = "";
    public Severity Severity { get; set; }
    public string Message { get; set; } = "";
    public DateTimeOffset RaisedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public int Occurrences { get; set; } = 1;
    public string? AcknowledgedBy { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? Resolution { get; set; }

    public bool IsOpen => ResolvedAt is null;
}

public static class AlertRules
{
    public const string PickupMissed = "PICKUP_MISSED";
    public const string DeliveryLate = "DELIVERY_LATE";
    public const string EtaSlipped = "ETA_SLIPPED";
    public const string TemperatureExcursion = "TEMP_EXCURSION";
    public const string ReeferDoorOpen = "REEFER_DOOR";
    public const string DangerousGoodsSegregation = "DG_SEGREGATION";
    public const string DangerousGoodsVehicle = "DG_VEHICLE";
    public const string CustomsNotReleased = "CUSTOMS_PENDING";
    public const string CustomsRejected = "CUSTOMS_REJECTED";
    public const string NoTrackingSignal = "NO_SIGNAL";
    public const string LongStop = "LONG_STOP";
    public const string ScanMismatch = "SCAN_MISMATCH";
    public const string Overweight = "OVERWEIGHT";
}

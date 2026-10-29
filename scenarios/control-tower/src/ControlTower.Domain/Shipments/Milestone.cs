namespace Proseware.ControlTower.Domain.Shipments;

public class Milestone
{
    public long Id { get; set; }
    public int ShipmentId { get; set; }
    public string Code { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public int? LocationId { get; set; }
    public string? LocationText { get; set; }
    /// <summary>Where the event came from: EDI partner id, "telematics", "scan:&lt;device&gt;", user name.</summary>
    public string Source { get; set; } = "";
    public string? Remarks { get; set; }
    public bool VisibleToCustomer { get; set; } = true;
}

/// <summary>Milestone codes. Carrier status codes are mapped onto these.</summary>
public static class MilestoneCodes
{
    public const string Booked = "BKD";
    public const string PickupPlanned = "PUP";
    public const string PickedUp = "PUC";
    public const string ArrivedAtTerminal = "ATT";
    public const string DepartedTerminal = "DTT";
    public const string Loaded = "LOD";
    public const string Departed = "DEP";
    public const string Arrived = "ARR";
    public const string Discharged = "DIS";
    public const string CustomsSubmitted = "CUS";
    public const string CustomsReleased = "CRL";
    public const string CustomsHeld = "CHD";
    public const string OutForDelivery = "OFD";
    public const string DeliveryAttempted = "DAT";
    public const string Delivered = "DLV";
    public const string Damaged = "DMG";
    public const string Cancelled = "CAN";
    public const string EtaChanged = "ETA";

    private static readonly Dictionary<string, string> Texts = new(StringComparer.Ordinal)
    {
        [Booked] = "Booked",
        [PickupPlanned] = "Pickup planned",
        [PickedUp] = "Picked up",
        [ArrivedAtTerminal] = "Arrived at terminal",
        [DepartedTerminal] = "Departed terminal",
        [Loaded] = "Loaded",
        [Departed] = "Departed",
        [Arrived] = "Arrived",
        [Discharged] = "Discharged",
        [CustomsSubmitted] = "Customs declaration submitted",
        [CustomsReleased] = "Released by customs",
        [CustomsHeld] = "Held by customs",
        [OutForDelivery] = "Out for delivery",
        [DeliveryAttempted] = "Delivery attempted",
        [Delivered] = "Delivered",
        [Damaged] = "Damage reported",
        [Cancelled] = "Cancelled",
        [EtaChanged] = "Estimated arrival changed",
    };

    public static IReadOnlyCollection<string> All => Texts.Keys;

    public static string Describe(string code) => Texts.TryGetValue(code, out var t) ? t : code;

    public static bool IsKnown(string code) => Texts.ContainsKey(code);
}

namespace Proseware.ControlTower.Domain.Scanning;

public enum ScanKind
{
    Receive = 1,
    PutAway = 2,
    Load = 3,
    Unload = 4,
    Deliver = 5,
    Damage = 6,
    Count = 7
}

public class ScanEvent
{
    public long Id { get; set; }
    public string Sscc { get; set; } = "";
    public int? PackageId { get; set; }
    public int? ShipmentId { get; set; }
    public ScanKind Kind { get; set; }
    public int TerminalId { get; set; }
    public string? DoorOrBay { get; set; }
    public string? VehiclePlate { get; set; }
    public string DeviceId { get; set; } = "";
    public string UserName { get; set; } = "";
    public DateTimeOffset ScannedAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public string? Remarks { get; set; }
    /// <summary>Client-side unique id; handhelds resend queued scans after losing Wi-Fi.</summary>
    public Guid ClientScanId { get; set; }
}

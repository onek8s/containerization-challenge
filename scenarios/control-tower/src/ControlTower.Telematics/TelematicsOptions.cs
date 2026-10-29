namespace Proseware.ControlTower.Telematics;

public sealed class TelematicsOptions
{
    public const string Section = "Telematics";

    public string BrokerHost { get; set; } = "mqtt01.corp.local";
    public int BrokerPort { get; set; } = 8883;
    public bool UseTls { get; set; } = true;
    public string ClientId { get; set; } = "ct-telematics";
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string TopicRoot { get; set; } = "proseware/telematics";
    public int BatchSize { get; set; } = 200;
    public int BatchSeconds { get; set; } = 5;
    /// <summary>Positions older than this are stored but not published as live events (unit back from a dead zone).</summary>
    public int LiveWindowMinutes { get; set; } = 15;
    public int VehicleCacheMinutes { get; set; } = 10;
    /// <summary>Hysteresis: a vehicle has left a geofence when it is this much further out than the radius.</summary>
    public int GeofenceExitMarginMeters { get; set; } = 150;
}

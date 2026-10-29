using NetTopologySuite.Geometries;

namespace Proseware.ControlTower.Domain.Fleet;

public class Vehicle
{
    public int Id { get; set; }
    public string Plate { get; set; } = "";
    public int? CarrierId { get; set; }
    /// <summary>Device id of the telematics unit (topic segment on the MQTT broker).</summary>
    public string? TelematicsUnitId { get; set; }
    public bool HasReefer { get; set; }
    public bool AdrCertified { get; set; }
    public decimal MaxPayloadKg { get; set; } = 24_000m;
    public decimal LoadingMeters { get; set; } = 13.6m;
    public DateTimeOffset? LastPositionAt { get; set; }
    public Point? LastPosition { get; set; }
    public decimal? LastTemperature { get; set; }
    public int? LastSpeedKmh { get; set; }
}

public class VehiclePosition
{
    public long Id { get; set; }
    public int VehicleId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public Point Position { get; set; } = null!;
    public int SpeedKmh { get; set; }
    public int HeadingDegrees { get; set; }
    public bool IgnitionOn { get; set; }
    public decimal? ReeferTemperature { get; set; }
    public decimal? ReeferSetpoint { get; set; }
    public bool? DoorOpen { get; set; }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Proseware.ControlTower.Telematics;

/// <summary>Position report of a telematics unit (topic proseware/telematics/&lt;unit&gt;/position).</summary>
public sealed class PositionMessage
{
    [JsonPropertyName("unit")] public string Unit { get; set; } = "";
    [JsonPropertyName("ts")] public DateTimeOffset Timestamp { get; set; }
    [JsonPropertyName("lat")] public double Latitude { get; set; }
    [JsonPropertyName("lon")] public double Longitude { get; set; }
    [JsonPropertyName("spd")] public int Speed { get; set; }
    [JsonPropertyName("hdg")] public int Heading { get; set; }
    [JsonPropertyName("ign")] public bool Ignition { get; set; }
    [JsonPropertyName("odo")] public long? OdometerKm { get; set; }
    [JsonPropertyName("sat")] public int? Satellites { get; set; }
}

/// <summary>Reefer unit report (topic proseware/telematics/&lt;unit&gt;/reefer).</summary>
public sealed class ReeferMessage
{
    [JsonPropertyName("unit")] public string Unit { get; set; } = "";
    [JsonPropertyName("ts")] public DateTimeOffset Timestamp { get; set; }
    [JsonPropertyName("t1")] public decimal? SupplyTemperature { get; set; }
    [JsonPropertyName("t2")] public decimal? ReturnTemperature { get; set; }
    [JsonPropertyName("sp")] public decimal? Setpoint { get; set; }
    [JsonPropertyName("door")] public bool? DoorOpen { get; set; }
    [JsonPropertyName("mode")] public string? Mode { get; set; }
}

public abstract record TelematicsReading(string UnitId, DateTimeOffset Timestamp);

public sealed record PositionReading(string UnitId, DateTimeOffset Timestamp, double Latitude, double Longitude, int SpeedKmh, int Heading, bool Ignition)
    : TelematicsReading(UnitId, Timestamp);

public sealed record ReeferReading(string UnitId, DateTimeOffset Timestamp, decimal? Temperature, decimal? Setpoint, bool? DoorOpen)
    : TelematicsReading(UnitId, Timestamp);

public static class TelematicsParser
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, NumberHandling = JsonNumberHandling.AllowReadingFromString };

    /// <summary>Topic layout: &lt;root&gt;/&lt;unit&gt;/&lt;kind&gt;.</summary>
    public static TelematicsReading? Parse(string topic, ReadOnlySpan<byte> payload)
    {
        var parts = topic.Split('/');
        if (parts.Length < 2) return null;
        var unitFromTopic = parts[^2];
        switch (parts[^1])
        {
            case "position":
                var p = JsonSerializer.Deserialize<PositionMessage>(payload, Options);
                if (p is null || Math.Abs(p.Latitude) > 90 || Math.Abs(p.Longitude) > 180 || (p.Latitude == 0 && p.Longitude == 0)) return null;
                return new PositionReading(string.IsNullOrEmpty(p.Unit) ? unitFromTopic : p.Unit, p.Timestamp, p.Latitude, p.Longitude, Math.Max(0, p.Speed), p.Heading, p.Ignition);
            case "reefer":
                var r = JsonSerializer.Deserialize<ReeferMessage>(payload, Options);
                if (r is null) return null;
                // the supply air sensor is the one we report; return air is the fallback
                return new ReeferReading(string.IsNullOrEmpty(r.Unit) ? unitFromTopic : r.Unit, r.Timestamp, r.SupplyTemperature ?? r.ReturnTemperature, r.Setpoint, r.DoorOpen);
            default:
                return null;
        }
    }
}

namespace Proseware.ControlTower.Routing;

public sealed class RoutingOptions
{
    public const string Section = "Routing";

    /// <summary>Itinero router database built from OpenStreetMap; empty uses great-circle estimates.</summary>
    public string? RouterDbPath { get; set; }
    /// <summary>Keep the router database on disk instead of loading it in memory (large extracts).</summary>
    public bool MemoryMapped { get; set; }
    public float ResolveRadiusMeters { get; set; } = 2_000f;

    public double RoadDetourFactor { get; set; } = 1.25;
    public double RoadAverageKmh { get; set; } = 68;
    public double SeaDetourFactor { get; set; } = 1.3;
    public double SeaAverageKmh { get; set; } = 30;
    public double AirAverageKmh { get; set; } = 750;
    public double AirHandlingHours { get; set; } = 3;
    public double RailDetourFactor { get; set; } = 1.2;
    public double RailAverageKmh { get; set; } = 50;

    /// <summary>Dwell time at a terminal between legs.</summary>
    public double TerminalHandlingHours { get; set; } = 2;
    /// <summary>Dwell time at a sea port between legs (discharge, gate out).</summary>
    public double PortHandlingHours { get; set; } = 36;
    public double AirportHandlingHours { get; set; } = 6;
}

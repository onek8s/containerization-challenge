using NetTopologySuite.Geometries;

namespace Proseware.ControlTower.Domain.Locations;

public class Location
{
    public int Id { get; set; }
    /// <summary>UN/LOCODE for ports, airports and terminals; customer site code otherwise.</summary>
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public LocationKind Kind { get; set; }
    public string Street { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string City { get; set; } = "";
    public string CountryCode { get; set; } = "";
    public Point Position { get; set; } = GeoPoints.Create(0, 0);
    /// <summary>Radius in metres used for arrival / departure detection from truck positions.</summary>
    public int GeofenceRadiusMeters { get; set; } = 500;
    public string TimeZoneId { get; set; } = "FLE Standard Time";
    public int? CustomerId { get; set; }
    public TimeSpan OpensAt { get; set; } = new(7, 0, 0);
    public TimeSpan ClosesAt { get; set; } = new(16, 0, 0);
    public bool IsEuCustomsTerritory { get; set; } = true;

    public string DisplayAddress => $"{Street}, {PostalCode} {City}, {CountryCode}";
}

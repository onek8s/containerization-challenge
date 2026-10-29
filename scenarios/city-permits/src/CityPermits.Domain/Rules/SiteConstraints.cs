namespace CityPermits.Domain;

/// <summary>Result of the site checks for a parcel or a proposed footprint.</summary>
public sealed class SiteConstraints
{
    public string ParcelId { get; set; } = "";
    public double ParcelAreaM2 { get; set; }
    public string? ZoningCode { get; set; }
    public string? ZoningName { get; set; }
    public double? MaxHeightM { get; set; }
    public double? MaxSiteCoverage { get; set; }
    public List<string> PermittedUseClasses { get; set; } = new();
    public List<string> HeritageAreas { get; set; } = new();
    public string? FloodZone { get; set; }
    public List<string> Easements { get; set; } = new();
    public double EasementOverlapM2 { get; set; }
    public double ExistingCoverageM2 { get; set; }
    public string? ParkingZone { get; set; }
    public string? Ward { get; set; }
    public double? DistanceToSchoolM { get; set; }
    public double? DistanceToNearestLicensedPremisesM { get; set; }
    public List<string> TreePreservationOrders { get; set; } = new();
    public string Source { get; set; } = "";
    public DateTime CheckedUtc { get; set; }
}

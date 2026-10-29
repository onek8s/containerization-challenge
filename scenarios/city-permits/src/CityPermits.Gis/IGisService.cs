using CityPermits.Contracts;
using CityPermits.Domain;

namespace CityPermits.Gis;

public sealed record RenderedImage(byte[] Content, string ContentType);

public sealed record ReplicaSyncResult(string Layer, int Inserted, int Updated, int Deleted, DateTime GenerationUtc);

/// <summary>City GIS: parcels, planning constraints, map rendering and replica maintenance.</summary>
public interface IGisService
{
    string ProviderName { get; }

    ParcelDto? GetParcel(string parcelId);

    ParcelDto? FindParcelAt(double easting, double northing);

    IReadOnlyList<ParcelDto> SearchParcels(string text, int max = 20);

    SiteConstraints CheckSite(SiteCheckRequest request);

    IReadOnlyList<IdentifyResultDto> Identify(double easting, double northing, double toleranceM, IReadOnlyCollection<string>? layers = null);

    /// <summary>Renders a 256x256 web mercator tile of the operational layers.</summary>
    RenderedImage RenderTile(string mapName, int z, int x, int y);

    /// <summary>Exports a layout (site location plan, decision map, inspection route) for a parcel.</summary>
    RenderedImage ExportMap(MapExportRequest request);

    /// <summary>Synchronises the local replica from the corporate geodatabase.</summary>
    IReadOnlyList<ReplicaSyncResult> SynchroniseReplica(CancellationToken cancellationToken);

    GisHealth Health();
}

public sealed record GisHealth(bool Ready, string Provider, string? LicenceLevel, string? LicenceStatus, string? ReplicaVersion, DateTime? ReplicaSyncedUtc, string? Message);

public static class Layers
{
    public const string Parcels = "Parcels";
    public const string Zoning = "ZoningDistricts";
    public const string Heritage = "ConservationAreas";
    public const string Flood = "FloodZones";
    public const string Easements = "Easements";
    public const string ParkingZones = "ParkingZones";
    public const string Schools = "Schools";
    public const string TreePreservation = "TreePreservationOrders";
    public const string LicensedPremises = "LicensedPremises";
    public const string Centrelines = "StreetCentrelines";

    public static readonly string[] Operational = { Parcels, Zoning, Heritage, Flood, Easements, ParkingZones, Schools, TreePreservation, LicensedPremises, Centrelines };
}

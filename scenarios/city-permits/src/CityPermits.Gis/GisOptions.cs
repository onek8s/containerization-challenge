namespace CityPermits.Gis;

public sealed class GisOptions
{
    public const string Section = "Gis";

    /// <summary>Geomatix (production) or Development (GeoJSON files, no cartography).</summary>
    public string Provider { get; set; } = "Geomatix";

    /// <summary>Geomatix Pro installation folder; the SDK assemblies are loaded from its bin folder.</summary>
    public string InstallDir { get; set; } = @"C:\Program Files\Geomatix\Pro";

    /// <summary>Licence product to check out: Basic, Standard or Advanced. Map export needs Standard.</summary>
    public string LicenceLevel { get; set; } = "Standard";

    /// <summary>Local file geodatabase replica (refreshed nightly by the jobs service).</summary>
    public string ReplicaPath { get; set; } = @"D:\Gis\Replica\Ashford.ggdb";

    /// <summary>Connection file for the corporate enterprise geodatabase on gis-sql.</summary>
    public string EnterpriseConnectionFile { get; set; } = @"D:\Gis\Connections\gis-sql_ASHFORD_GIS.gconn";

    /// <summary>Map document with symbology and scale ranges (maintained by the GIS team).</summary>
    public string MapDocument { get; set; } = @"D:\Gis\Maps\Permits.gmapx";

    /// <summary>Folder with layout templates (*.glytx): SiteLocation, DecisionMap, InspectionRoute.</summary>
    public string LayoutFolder { get; set; } = @"D:\Gis\Layouts";

    /// <summary>Basemap tile package (Ordnance Survey MasterMap, PSGA licence) used under the operational layers.</summary>
    public string BasemapPackage { get; set; } = @"D:\Gis\Basemap\OSMasterMap_Ashford.gtpk";

    /// <summary>Folder with GeoJSON layers for the Development provider.</summary>
    public string DevelopmentDataPath { get; set; } = "../../samples/gis";

    public int TileCacheMinutes { get; set; } = 60;

    public int EngineTimeoutSeconds { get; set; } = 60;
}

using System.Runtime.Versioning;
using CityPermits.Contracts;
using CityPermits.Domain;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CityPermits.Gis.Geomatix;

/// <summary>Production GIS: queries the local replica and renders with the GIS team's map document and layouts.</summary>
[SupportedOSPlatform("windows")]
public sealed class GeomatixGisService : IGisService
{
    private const int BritishNationalGrid = 27700;
    private const int WebMercator = 3857;

    private readonly GeomatixRuntime _rt;
    private readonly GisOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GeomatixGisService> _logger;
    private DateTime? _syncedUtc;

    public GeomatixGisService(GeomatixRuntime runtime, IOptions<GisOptions> options, IMemoryCache cache, ILogger<GeomatixGisService> logger)
    {
        _rt = runtime;
        _options = options.Value;
        _cache = cache;
        _logger = logger;
    }

    public string ProviderName => "Geomatix";

    public ParcelDto? GetParcel(string parcelId) => _rt.Invoke(rt =>
    {
        using var fc = (IDisposable)rt.Replica!.OpenFeatureClass(Layers.Parcels);
        dynamic d = fc;
        var filter = rt.NewObject("Geomatix.Core.Data.QueryFilter");
        filter.WhereClause = $"PARCEL_ID = '{parcelId.Replace("'", "''")}'";
        using dynamic cursor = d.Search(filter, true);
        return cursor.MoveNext() ? ToParcel(rt, cursor.Current) : null;
    });

    public ParcelDto? FindParcelAt(double easting, double northing) => _rt.Invoke(rt =>
    {
        var point = rt.Static(rt.Core, "Geomatix.Core.Geometry.MapPointBuilder", "CreateMapPoint", easting, northing, rt.SpatialReference(BritishNationalGrid));
        using dynamic cursor = SpatialSearch(rt, Layers.Parcels, point, "Intersects");
        return cursor.MoveNext() ? ToParcel(rt, cursor.Current) : null;
    });

    public IReadOnlyList<ParcelDto> SearchParcels(string text, int max = 20) => _rt.Invoke(rt =>
    {
        using var fc = (IDisposable)rt.Replica!.OpenFeatureClass(Layers.Parcels);
        dynamic d = fc;
        var filter = rt.NewObject("Geomatix.Core.Data.QueryFilter");
        filter.WhereClause = $"UPPER(ADDRESS) LIKE '%{text.ToUpperInvariant().Replace("'", "''")}%' OR UPRN = '{text.Replace("'", "''")}'";
        filter.RowCount = max;
        using dynamic cursor = d.Search(filter, true);
        var list = new List<ParcelDto>();
        while (cursor.MoveNext() && list.Count < max) list.Add(ToParcel(rt, cursor.Current));
        return (IReadOnlyList<ParcelDto>)list;
    });

    public SiteConstraints CheckSite(SiteCheckRequest request) => _rt.Invoke(rt =>
    {
        var geo = rt.Geometry;
        dynamic parcel = FindFeature(rt, Layers.Parcels, "PARCEL_ID", request.ParcelId)
            ?? throw new KeyNotFoundException($"Parcel {request.ParcelId} not found.");
        dynamic parcelShape = parcel.GetShape();
        dynamic footprint = string.IsNullOrEmpty(request.FootprintWkt)
            ? parcelShape
            : rt.Static(rt.Core, "Geomatix.Core.Geometry.GeometryEngine", "ImportFromWKT", 0, request.FootprintWkt, rt.SpatialReference(BritishNationalGrid));
        dynamic buffered = geo.Buffer(footprint, request.BufferM);

        var s = new SiteConstraints
        {
            ParcelId = request.ParcelId,
            ParcelAreaM2 = (double)geo.Area(parcelShape),
            ExistingCoverageM2 = Convert.ToDouble(parcel["BUILT_M2"] ?? 0d),
            Ward = (string?)parcel["WARD"],
            Source = $"Geomatix {rt.EngineVersion}",
            CheckedUtc = DateTime.UtcNow
        };

        using (dynamic c = SpatialSearch(rt, Layers.Zoning, geo.LabelPoint(parcelShape), "Within"))
            if (c.MoveNext())
            {
                dynamic z = c.Current;
                s.ZoningCode = (string)z["ZONE_CODE"];
                s.ZoningName = (string)z["ZONE_NAME"];
                s.MaxHeightM = (double?)z["MAX_HEIGHT_M"];
                s.MaxSiteCoverage = (double?)z["MAX_COVERAGE"];
                s.PermittedUseClasses = ((string?)z["PERMITTED_USES"] ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
            }

        using (dynamic c = SpatialSearch(rt, Layers.Heritage, footprint, "Intersects"))
            while (c.MoveNext()) s.HeritageAreas.Add((string)c.Current["NAME"]);

        using (dynamic c = SpatialSearch(rt, Layers.Flood, footprint, "Intersects"))
            while (c.MoveNext())
            {
                var zone = (string)c.Current["FLOOD_ZONE"];
                if (s.FloodZone is null || string.CompareOrdinal(zone, s.FloodZone) > 0) s.FloodZone = zone;
            }

        using (dynamic c = SpatialSearch(rt, Layers.Easements, buffered, "Intersects"))
            while (c.MoveNext())
            {
                dynamic e = c.Current;
                s.Easements.Add($"{e["EASEMENT_ID"]} {e["TYPE"]}");
                if (!string.IsNullOrEmpty(request.FootprintWkt))
                    s.EasementOverlapM2 += (double)geo.Area(geo.Intersection(footprint, e.GetShape()));
            }

        using (dynamic c = SpatialSearch(rt, Layers.ParkingZones, geo.LabelPoint(parcelShape), "Within"))
            if (c.MoveNext()) s.ParkingZone = (string)c.Current["ZONE_CODE"];

        using (dynamic c = SpatialSearch(rt, Layers.TreePreservation, parcelShape, "Contains"))
            while (c.MoveNext()) s.TreePreservationOrders.Add((string)c.Current["TPO_REF"]);

        s.DistanceToSchoolM = NearestDistance(rt, Layers.Schools, parcelShape, 2000);
        s.DistanceToNearestLicensedPremisesM = NearestDistance(rt, Layers.LicensedPremises, parcelShape, 500);
        return s;
    });

    public IReadOnlyList<IdentifyResultDto> Identify(double easting, double northing, double toleranceM, IReadOnlyCollection<string>? layers = null) => _rt.Invoke(rt =>
    {
        var point = rt.Static(rt.Core, "Geomatix.Core.Geometry.MapPointBuilder", "CreateMapPoint", easting, northing, rt.SpatialReference(BritishNationalGrid));
        dynamic area = rt.Geometry.Buffer(point, toleranceM);
        var results = new List<IdentifyResultDto>();
        foreach (var layer in layers ?? Layers.Operational)
        {
            using dynamic c = SpatialSearch(rt, layer, area, "Intersects");
            while (c.MoveNext())
            {
                dynamic row = c.Current;
                var attrs = new Dictionary<string, string?>();
                foreach (dynamic field in row.GetFields())
                    if ((string)field.FieldType != "Geometry") attrs[(string)field.Name] = row[(string)field.Name]?.ToString();
                results.Add(new IdentifyResultDto(layer, row.GetObjectID().ToString(), attrs));
            }
        }
        return (IReadOnlyList<IdentifyResultDto>)results;
    });

    public RenderedImage RenderTile(string mapName, int z, int x, int y) =>
        _cache.GetOrCreate($"tile:{mapName}:{z}:{x}:{y}", e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.TileCacheMinutes);
            e.Size = 1;
            return _rt.Invoke(rt =>
            {
                var b = Coordinates.TileBounds(z, x, y);
                var (minX, minY) = Coordinates.ToMercator(b.MinLat, b.MinLon);
                var (maxX, maxY) = Coordinates.ToMercator(b.MaxLat, b.MaxLon);
                dynamic map = rt.MapDocument!.FindMap(mapName);
                dynamic extent = rt.Static(rt.Core, "Geomatix.Core.Geometry.EnvelopeBuilder", "CreateEnvelope", minX, minY, maxX, maxY, rt.SpatialReference(WebMercator));
                dynamic export = rt.NewObject("Geomatix.Mapping.PngExportFormat");
                export.Width = 256;
                export.Height = 256;
                export.Resolution = 96;
                export.HasTransparentBackground = true;
                byte[] bytes = map.ExportToBytes(extent, export);
                return new RenderedImage(bytes, "image/png");
            });
        })!;

    public RenderedImage ExportMap(MapExportRequest request) => _rt.Invoke(rt =>
    {
        var template = Path.Combine(_options.LayoutFolder, request.Template + ".glytx");
        dynamic layout = rt.Static(rt.Mapping, "Geomatix.Mapping.LayoutFactory", "CreateFromTemplate", template, rt.MapDocument);
        try
        {
            dynamic parcel = FindFeature(rt, Layers.Parcels, "PARCEL_ID", request.ParcelId)
                ?? throw new KeyNotFoundException($"Parcel {request.ParcelId} not found.");
            dynamic frame = layout.FindElement("Main Map Frame");
            frame.ZoomTo(rt.Geometry.Expand(parcel.GetShape().Extent, 2.5, 2.5, true));
            if (!string.IsNullOrEmpty(request.FootprintWkt))
                frame.Map.AddGraphic(rt.Static(rt.Core, "Geomatix.Core.Geometry.GeometryEngine", "ImportFromWKT", 0, request.FootprintWkt, rt.SpatialReference(BritishNationalGrid)), "Proposed footprint");
            layout.FindElement("Title").Text = request.Title;
            layout.FindElement("Reference").Text = request.Reference ?? "";
            layout.FindElement("Copyright").Text = $"© Crown copyright and database rights {DateTime.UtcNow.Year} OS AC0000851297";

            dynamic format = request.Format.Equals("pdf", StringComparison.OrdinalIgnoreCase)
                ? rt.NewObject("Geomatix.Mapping.PdfExportFormat")
                : rt.NewObject("Geomatix.Mapping.PngExportFormat");
            format.Resolution = request.Dpi;
            byte[] bytes = layout.ExportToBytes(format);
            return new RenderedImage(bytes, request.Format.Equals("pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : "image/png");
        }
        finally
        {
            (layout as IDisposable)?.Dispose();
        }
    });

    /// <summary>
    /// Pulls changes from the corporate geodatabase into the local replica (one-way replica
    /// "PERMITS_<host>", registered by the GIS team) and reopens it.
    /// </summary>
    public IReadOnlyList<ReplicaSyncResult> SynchroniseReplica(CancellationToken cancellationToken)
    {
        var results = _rt.Invoke(rt =>
        {
            using var enterprise = (IDisposable)rt.OpenEnterpriseGeodatabase(_options.EnterpriseConnectionFile);
            dynamic source = enterprise;
            dynamic replica = source.GetReplica("PERMITS_" + Environment.MachineName.ToUpperInvariant());
            dynamic options = rt.NewObject("Geomatix.Core.Data.Replication.SynchronizeOptions");
            options.Direction = Enum.Parse(rt.Core.GetType("Geomatix.Core.Data.Replication.SynchronizeDirection", true)!, "ParentToChild");
            options.ConflictPolicy = Enum.Parse(rt.Core.GetType("Geomatix.Core.Data.Replication.ConflictPolicy", true)!, "FavorParent");
            dynamic report = replica.Synchronize(rt.Replica, options);
            var list = new List<ReplicaSyncResult>();
            foreach (dynamic layer in report.Layers)
                list.Add(new ReplicaSyncResult((string)layer.Name, (int)layer.Inserted, (int)layer.Updated, (int)layer.Deleted, (DateTime)report.GenerationUtc));
            return list;
        });
        _rt.ReopenReplica();
        if (_cache is MemoryCache mc) mc.Compact(1.0);
        _syncedUtc = DateTime.UtcNow;
        _logger.LogInformation("Replica synchronised: {Layers}", string.Join(", ", results.Select(r => $"{r.Layer} +{r.Inserted}/~{r.Updated}/-{r.Deleted}")));
        return results;
    }

    public GisHealth Health()
    {
        try
        {
            return _rt.Invoke(rt => new GisHealth(true, ProviderName, _options.LicenceLevel, rt.LicenceStatus,
                (string?)rt.Replica!.GetVersion()?.ToString(), _syncedUtc, rt.EngineVersion));
        }
        catch (Exception ex)
        {
            return new GisHealth(false, ProviderName, _options.LicenceLevel, null, null, _syncedUtc, ex.Message);
        }
    }

    private static dynamic SpatialSearch(GeomatixRuntime rt, string layer, object geometry, string relationship)
    {
        dynamic fc = rt.Replica!.OpenFeatureClass(layer);
        dynamic filter = rt.NewObject("Geomatix.Core.Data.SpatialQueryFilter");
        filter.FilterGeometry = geometry;
        filter.SpatialRelationship = Enum.Parse(rt.Core.GetType("Geomatix.Core.Data.SpatialRelationship", true)!, relationship);
        return fc.Search(filter, true);
    }

    private static dynamic? FindFeature(GeomatixRuntime rt, string layer, string field, string value)
    {
        dynamic fc = rt.Replica!.OpenFeatureClass(layer);
        dynamic filter = rt.NewObject("Geomatix.Core.Data.QueryFilter");
        filter.WhereClause = $"{field} = '{value.Replace("'", "''")}'";
        using dynamic c = fc.Search(filter, false);
        return c.MoveNext() ? c.Current : null;
    }

    private static double? NearestDistance(GeomatixRuntime rt, string layer, object shape, double searchM)
    {
        dynamic area = rt.Geometry.Buffer(shape, searchM);
        double? best = null;
        using dynamic c = SpatialSearch(rt, layer, area, "Intersects");
        while (c.MoveNext())
        {
            double d = rt.Geometry.Distance(shape, c.Current.GetShape());
            if (best is null || d < best) best = d;
        }
        return best;
    }

    private static ParcelDto ToParcel(GeomatixRuntime rt, dynamic row)
    {
        dynamic shape = row.GetShape();
        dynamic label = rt.Geometry.LabelPoint(shape);
        double e = label.X, n = label.Y;
        var (lat, lon) = Coordinates.ToLatLon(e, n);
        return new ParcelDto((string)row["PARCEL_ID"], (string?)row["UPRN"], (string)row["ADDRESS"], (double)rt.Geometry.Area(shape), e, n, lat, lon, null, (string?)row["WARD"]);
    }
}

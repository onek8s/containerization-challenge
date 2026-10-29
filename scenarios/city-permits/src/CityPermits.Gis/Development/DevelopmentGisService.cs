using CityPermits.Contracts;
using CityPermits.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using NtsPoint = NetTopologySuite.Geometries.Point;
using NtsPolygon = NetTopologySuite.Geometries.Polygon;

namespace CityPermits.Gis.Development;

/// <summary>
/// Developer workstation provider: reads the GeoJSON extracts in samples/gis and draws plain
/// outlines. No basemap, symbology or layouts; output is not suitable for decision notices.
/// </summary>
public sealed class DevelopmentGisService : IGisService
{
    private readonly Dictionary<string, List<IFeature>> _layers = new();

    private static readonly Dictionary<string, (string File, Rgba32 Colour)> Files = new()
    {
        [Layers.Parcels] = ("parcels", new Rgba32(90, 90, 90, 255)),
        [Layers.Zoning] = ("zoning", new Rgba32(230, 159, 0, 255)),
        [Layers.Heritage] = ("heritage", new Rgba32(160, 60, 180, 255)),
        [Layers.Flood] = ("flood", new Rgba32(0, 114, 178, 255)),
        [Layers.Easements] = ("easements", new Rgba32(213, 94, 0, 255)),
        [Layers.ParkingZones] = ("parking_zones", new Rgba32(0, 158, 115, 255)),
        [Layers.Schools] = ("schools", new Rgba32(204, 121, 167, 255)),
        [Layers.TreePreservation] = ("tpo", new Rgba32(30, 120, 30, 255)),
        [Layers.LicensedPremises] = ("licensed_premises", new Rgba32(180, 30, 30, 255)),
        [Layers.Centrelines] = ("centrelines", new Rgba32(120, 120, 120, 255)),
    };

    public DevelopmentGisService(IOptions<GisOptions> options, IHostEnvironment env)
    {
        var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(env.ContentRootPath, options.Value.DevelopmentDataPath));
        var reader = new GeoJsonReader();
        foreach (var (layer, (file, _)) in Files)
        {
            var path = System.IO.Path.Combine(dir, file + ".geojson");
            _layers[layer] = File.Exists(path)
                ? reader.Read<FeatureCollection>(File.ReadAllText(path)).ToList()
                : new List<IFeature>();
        }
    }

    public string ProviderName => "Development";

    private IEnumerable<IFeature> L(string layer) => _layers.TryGetValue(layer, out var l) ? l : Enumerable.Empty<IFeature>();

    private static string? A(IFeature f, string name) => f.Attributes.Exists(name) ? f.Attributes[name]?.ToString() : null;

    public ParcelDto? GetParcel(string parcelId) =>
        L(Layers.Parcels).Where(f => A(f, "PARCEL_ID") == parcelId).Select(ToParcel).FirstOrDefault();

    public ParcelDto? FindParcelAt(double easting, double northing)
    {
        var p = new NtsPoint(easting, northing);
        return L(Layers.Parcels).Where(f => f.Geometry.Contains(p)).Select(ToParcel).FirstOrDefault();
    }

    public IReadOnlyList<ParcelDto> SearchParcels(string text, int max = 20) =>
        L(Layers.Parcels).Where(f => (A(f, "ADDRESS") ?? "").Contains(text, StringComparison.OrdinalIgnoreCase) || A(f, "UPRN") == text)
            .Take(max).Select(ToParcel).ToList();

    public SiteConstraints CheckSite(SiteCheckRequest request)
    {
        var parcel = L(Layers.Parcels).FirstOrDefault(f => A(f, "PARCEL_ID") == request.ParcelId)
            ?? throw new KeyNotFoundException($"Parcel {request.ParcelId} not found.");
        var footprint = string.IsNullOrEmpty(request.FootprintWkt) ? parcel.Geometry : new WKTReader().Read(request.FootprintWkt);
        var centre = parcel.Geometry.InteriorPoint;
        var buffered = footprint.Buffer(request.BufferM);
        var s = new SiteConstraints
        {
            ParcelId = request.ParcelId,
            ParcelAreaM2 = parcel.Geometry.Area,
            ExistingCoverageM2 = double.TryParse(A(parcel, "BUILT_M2"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var built) ? built : 0,
            Ward = A(parcel, "WARD"),
            Source = "Development",
            CheckedUtc = DateTime.UtcNow
        };
        var zone = L(Layers.Zoning).FirstOrDefault(f => f.Geometry.Contains(centre));
        if (zone is not null)
        {
            s.ZoningCode = A(zone, "ZONE_CODE");
            s.ZoningName = A(zone, "ZONE_NAME");
            s.MaxHeightM = Convert.ToDouble(zone.Attributes["MAX_HEIGHT_M"]);
            s.MaxSiteCoverage = Convert.ToDouble(zone.Attributes["MAX_COVERAGE"]);
            s.PermittedUseClasses = (A(zone, "PERMITTED_USES") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        }
        s.HeritageAreas = L(Layers.Heritage).Where(f => f.Geometry.Intersects(footprint)).Select(f => A(f, "NAME")!).ToList();
        s.FloodZone = L(Layers.Flood).Where(f => f.Geometry.Intersects(footprint)).Select(f => A(f, "FLOOD_ZONE")!).OrderByDescending(z => z, StringComparer.Ordinal).FirstOrDefault();
        foreach (var e in L(Layers.Easements).Where(f => f.Geometry.Intersects(buffered)))
        {
            s.Easements.Add($"{A(e, "EASEMENT_ID")} {A(e, "TYPE")}");
            if (!string.IsNullOrEmpty(request.FootprintWkt)) s.EasementOverlapM2 += footprint.Intersection(e.Geometry).Area;
        }
        s.ParkingZone = L(Layers.ParkingZones).Where(f => f.Geometry.Contains(centre)).Select(f => A(f, "ZONE_CODE")).FirstOrDefault();
        s.TreePreservationOrders = L(Layers.TreePreservation).Where(f => parcel.Geometry.Contains(f.Geometry)).Select(f => A(f, "TPO_REF")!).ToList();
        s.DistanceToSchoolM = L(Layers.Schools).Select(f => (double?)f.Geometry.Distance(parcel.Geometry)).Where(d => d <= 2000).Min();
        s.DistanceToNearestLicensedPremisesM = L(Layers.LicensedPremises).Select(f => (double?)f.Geometry.Distance(parcel.Geometry)).Where(d => d <= 500).Min();
        return s;
    }

    public IReadOnlyList<IdentifyResultDto> Identify(double easting, double northing, double toleranceM, IReadOnlyCollection<string>? layers = null)
    {
        var area = new NtsPoint(easting, northing).Buffer(toleranceM);
        var result = new List<IdentifyResultDto>();
        foreach (var layer in layers ?? Layers.Operational)
            foreach (var (f, i) in L(layer).Select((f, i) => (f, i)).Where(x => x.f.Geometry.Intersects(area)))
                result.Add(new IdentifyResultDto(layer, i.ToString(), f.Attributes.GetNames().ToDictionary(n => n, n => f.Attributes[n]?.ToString())));
        return result;
    }

    public RenderedImage RenderTile(string mapName, int z, int x, int y)
    {
        var b = Coordinates.TileBounds(z, x, y);
        var (minE, minN) = Coordinates.FromLatLon(b.MinLat, b.MinLon);
        var (maxE, maxN) = Coordinates.FromLatLon(b.MaxLat, b.MaxLon);
        return Draw(256, 256, new Envelope(minE, maxE, minN, maxN), null, transparent: true);
    }

    public RenderedImage ExportMap(MapExportRequest request)
    {
        var parcel = L(Layers.Parcels).FirstOrDefault(f => A(f, "PARCEL_ID") == request.ParcelId)
            ?? throw new KeyNotFoundException($"Parcel {request.ParcelId} not found.");
        var env = new Envelope(parcel.Geometry.EnvelopeInternal);
        env.ExpandBy(Math.Max(env.Width, env.Height) * 2.5);
        var footprint = string.IsNullOrEmpty(request.FootprintWkt) ? null : new WKTReader().Read(request.FootprintWkt);
        return Draw(800, 600, env, footprint ?? parcel.Geometry, transparent: false);
    }

    private RenderedImage Draw(int width, int height, Envelope env, Geometry? highlight, bool transparent)
    {
        using var img = new Image<Rgba32>(width, height, transparent ? Color.Transparent : Color.White);
        PointF P(Coordinate c) => new((float)((c.X - env.MinX) / env.Width * width), (float)((env.MaxY - c.Y) / env.Height * height));
        var envGeom = new GeometryFactory().ToGeometry(env);
        img.Mutate(ctx =>
        {
            foreach (var (layer, (_, colour)) in Files)
                foreach (var f in L(layer).Where(f => f.Geometry.Intersects(envGeom)))
                    DrawGeometry(ctx, f.Geometry, colour, P, layer == Layers.Parcels ? 1f : 2f);
            if (highlight is not null) DrawGeometry(ctx, highlight, new Rgba32(220, 0, 0, 255), P, 3f);
        });
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return new RenderedImage(ms.ToArray(), "image/png");
    }

    private static void DrawGeometry(IImageProcessingContext ctx, Geometry g, Rgba32 colour, Func<Coordinate, PointF> p, float width)
    {
        switch (g)
        {
            case NtsPolygon poly:
                ctx.DrawPolygon(colour, width, poly.ExteriorRing.Coordinates.Select(p).ToArray());
                break;
            case LineString line:
                ctx.DrawLine(colour, width, line.Coordinates.Select(p).ToArray());
                break;
            case NtsPoint pt:
                ctx.Fill(colour, new EllipsePolygon(p(pt.Coordinate), 5));
                break;
            case GeometryCollection gc:
                foreach (var part in gc.Geometries) DrawGeometry(ctx, part, colour, p, width);
                break;
        }
    }

    public IReadOnlyList<ReplicaSyncResult> SynchroniseReplica(CancellationToken cancellationToken) =>
        _layers.Select(l => new ReplicaSyncResult(l.Key, 0, 0, 0, DateTime.UtcNow)).ToList();

    public GisHealth Health() => new(true, ProviderName, null, "Development", "samples", null, $"{_layers.Sum(l => l.Value.Count)} features");

    private static ParcelDto ToParcel(IFeature f)
    {
        var c = f.Geometry.InteriorPoint;
        var (lat, lon) = Coordinates.ToLatLon(c.X, c.Y);
        return new ParcelDto(A(f, "PARCEL_ID")!, A(f, "UPRN"), A(f, "ADDRESS") ?? "", f.Geometry.Area, c.X, c.Y, lat, lon, null, A(f, "WARD"));
    }
}

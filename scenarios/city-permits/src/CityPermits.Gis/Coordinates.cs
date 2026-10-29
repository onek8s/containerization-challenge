using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace CityPermits.Gis;

/// <summary>British National Grid (EPSG:27700) to WGS84 / Web Mercator.</summary>
public static class Coordinates
{
    private const string Bng = "PROJCS[\"OSGB 1936 / British National Grid\",GEOGCS[\"OSGB 1936\",DATUM[\"OSGB_1936\",SPHEROID[\"Airy 1830\",6377563.396,299.3249646],TOWGS84[446.448,-125.157,542.06,0.15,0.247,0.842,-20.489]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"latitude_of_origin\",49],PARAMETER[\"central_meridian\",-2],PARAMETER[\"scale_factor\",0.9996012717],PARAMETER[\"false_easting\",400000],PARAMETER[\"false_northing\",-100000],UNIT[\"metre\",1],AUTHORITY[\"EPSG\",\"27700\"]]";

    private static readonly ICoordinateTransformation ToWgs;
    private static readonly ICoordinateTransformation FromWgs;
    private static readonly ICoordinateTransformation WgsToMercator;
    private static readonly ICoordinateTransformation MercatorToWgs;

    static Coordinates()
    {
        var csf = new CoordinateSystemFactory();
        var ctf = new CoordinateTransformationFactory();
        var bng = csf.CreateFromWkt(Bng);
        var wgs = GeographicCoordinateSystem.WGS84;
        var merc = ProjectedCoordinateSystem.WebMercator;
        ToWgs = ctf.CreateFromCoordinateSystems(bng, wgs);
        FromWgs = ctf.CreateFromCoordinateSystems(wgs, bng);
        WgsToMercator = ctf.CreateFromCoordinateSystems(wgs, merc);
        MercatorToWgs = ctf.CreateFromCoordinateSystems(merc, wgs);
    }

    public static (double Lat, double Lon) ToLatLon(double easting, double northing)
    {
        var r = ToWgs.MathTransform.Transform(new[] { easting, northing });
        return (r[1], r[0]);
    }

    public static (double Easting, double Northing) FromLatLon(double lat, double lon)
    {
        var r = FromWgs.MathTransform.Transform(new[] { lon, lat });
        return (r[0], r[1]);
    }

    public static (double X, double Y) ToMercator(double lat, double lon)
    {
        var r = WgsToMercator.MathTransform.Transform(new[] { lon, lat });
        return (r[0], r[1]);
    }

    public static (double MinLon, double MinLat, double MaxLon, double MaxLat) TileBounds(int z, int x, int y)
    {
        double n = Math.Pow(2, z);
        static double Lat(double yy, double nn) => Math.Atan(Math.Sinh(Math.PI * (1 - 2 * yy / nn))) * 180 / Math.PI;
        return (x / n * 360 - 180, Lat(y + 1, n), (x + 1) / n * 360 - 180, Lat(y, n));
    }
}

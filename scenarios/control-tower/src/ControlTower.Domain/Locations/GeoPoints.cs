using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace Proseware.ControlTower.Domain.Locations;

/// <summary>WGS 84 helpers. NetTopologySuite stores X = longitude, Y = latitude.</summary>
public static class GeoPoints
{
    public const int Wgs84 = 4326;
    private const double EarthRadiusMeters = 6_371_008.8;

    public static readonly GeometryFactory Factory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: Wgs84);

    public static Point Create(double latitude, double longitude) =>
        Factory.CreatePoint(new Coordinate(longitude, latitude));

    public static double Latitude(this Point p) => p.Y;

    public static double Longitude(this Point p) => p.X;

    public static double DistanceMeters(Point a, Point b) =>
        DistanceMeters(a.Y, a.X, b.Y, b.X);

    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    public static double BearingDegrees(Point from, Point to)
    {
        var lat1 = ToRadians(from.Y);
        var lat2 = ToRadians(to.Y);
        var dLon = ToRadians(to.X - from.X);
        var y = Math.Sin(dLon) * Math.Cos(lat2);
        var x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
        return (ToDegrees(Math.Atan2(y, x)) + 360) % 360;
    }

    public static bool IsInside(Point position, Location location) =>
        DistanceMeters(position, location.Position) <= location.GeofenceRadiusMeters;

    private static double ToRadians(double deg) => deg * Math.PI / 180d;

    private static double ToDegrees(double rad) => rad * 180d / Math.PI;
}

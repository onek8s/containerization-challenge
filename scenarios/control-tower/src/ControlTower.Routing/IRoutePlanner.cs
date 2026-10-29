using Proseware.ControlTower.Domain;

namespace Proseware.ControlTower.Routing;

public readonly record struct GeoCoordinate(double Latitude, double Longitude);

public sealed record RoutePlan(double DistanceKm, TimeSpan Duration, IReadOnlyList<GeoCoordinate> Shape, bool Estimated);

public interface IRoutePlanner
{
    RoutePlan Plan(GeoCoordinate from, GeoCoordinate to, TransportMode mode);
}

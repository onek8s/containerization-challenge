using Microsoft.Extensions.Options;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Locations;

namespace Proseware.ControlTower.Routing;

/// <summary>Distance along the great circle times a detour factor, at an average speed per mode.</summary>
public sealed class GreatCircleRoutePlanner : IRoutePlanner
{
    private readonly RoutingOptions _options;

    public GreatCircleRoutePlanner(IOptions<RoutingOptions> options) => _options = options.Value;

    public RoutePlan Plan(GeoCoordinate from, GeoCoordinate to, TransportMode mode)
    {
        var straightKm = GeoPoints.DistanceMeters(from.Latitude, from.Longitude, to.Latitude, to.Longitude) / 1000d;
        var (factor, speed, extraHours) = mode switch
        {
            TransportMode.Sea => (_options.SeaDetourFactor, _options.SeaAverageKmh, 0d),
            TransportMode.Air => (1.05, _options.AirAverageKmh, _options.AirHandlingHours),
            TransportMode.Rail => (_options.RailDetourFactor, _options.RailAverageKmh, 0d),
            _ => (_options.RoadDetourFactor, _options.RoadAverageKmh, 0d)
        };
        var km = straightKm * factor;
        var hours = km / speed + extraHours;
        return new RoutePlan(Math.Round(km, 1), TimeSpan.FromHours(hours), new[] { from, to }, Estimated: true);
    }
}

using Itinero;
using Itinero.Osm.Vehicles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proseware.ControlTower.Domain;

namespace Proseware.ControlTower.Routing;

/// <summary>
/// Road routing on the OpenStreetMap router database (Itinero, car profile). Other modes and points
/// that cannot be resolved on the road network fall back to the great-circle planner.
/// </summary>
public sealed class ItineroRoutePlanner : IRoutePlanner, IDisposable
{
    private readonly RoutingOptions _options;
    private readonly GreatCircleRoutePlanner _fallback;
    private readonly ILogger<ItineroRoutePlanner> _logger;
    private readonly Lazy<Router> _router;
    private Stream? _stream;

    public ItineroRoutePlanner(IOptions<RoutingOptions> options, GreatCircleRoutePlanner fallback, ILogger<ItineroRoutePlanner> logger)
    {
        _options = options.Value;
        _fallback = fallback;
        _logger = logger;
        _router = new Lazy<Router>(Load, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public RoutePlan Plan(GeoCoordinate from, GeoCoordinate to, TransportMode mode)
    {
        if (mode != TransportMode.Road) return _fallback.Plan(from, to, mode);

        var router = _router.Value;
        var profile = Vehicle.Car.Fastest();
        var a = router.TryResolve(profile, (float)from.Latitude, (float)from.Longitude, _options.ResolveRadiusMeters);
        var b = router.TryResolve(profile, (float)to.Latitude, (float)to.Longitude, _options.ResolveRadiusMeters);
        if (a.IsError || b.IsError)
        {
            _logger.LogDebug("Could not resolve {From} or {To} on the road network", from, to);
            return _fallback.Plan(from, to, mode);
        }

        var route = router.TryCalculate(profile, a.Value, b.Value);
        if (route.IsError)
        {
            _logger.LogDebug("No road route {From} -> {To}: {Error}", from, to, route.ErrorMessage);
            return _fallback.Plan(from, to, mode);
        }

        var shape = route.Value.Shape.Select(c => new GeoCoordinate(c.Latitude, c.Longitude)).ToList();
        // Itinero's car profile assumes free-flowing traffic; trucks are slower.
        var truckFactor = 1.18;
        return new RoutePlan(
            Math.Round(route.Value.TotalDistance / 1000d, 1),
            TimeSpan.FromSeconds(route.Value.TotalTime * truckFactor),
            Simplify(shape, 200),
            Estimated: false);
    }

    public void Dispose() => _stream?.Dispose();

    private Router Load()
    {
        var path = _options.RouterDbPath!;
        var started = DateTime.UtcNow;
        RouterDb db;
        if (_options.MemoryMapped)
        {
            _stream = File.OpenRead(path);
            db = RouterDb.Deserialize(_stream, RouterDbProfile.NoCache);
        }
        else
        {
            using var stream = File.OpenRead(path);
            db = RouterDb.Deserialize(stream);
        }
        _logger.LogInformation("Router database {Path} loaded in {Seconds:0.0} s", path, (DateTime.UtcNow - started).TotalSeconds);
        return new Router(db);
    }

    private static IReadOnlyList<GeoCoordinate> Simplify(List<GeoCoordinate> shape, int maxPoints)
    {
        if (shape.Count <= maxPoints) return shape;
        var step = (double)(shape.Count - 1) / (maxPoints - 1);
        return Enumerable.Range(0, maxPoints).Select(i => shape[(int)Math.Round(i * step)]).ToList();
    }
}

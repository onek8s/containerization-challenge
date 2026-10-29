using Proseware.ControlTower.Routing;
using Xunit;

namespace Proseware.ControlTower.Tests.Routing;

public class RunOptimizerTests
{
    // straight-line distances on a grid, 1 unit = 1 km, 60 km/h
    private static (double, TimeSpan) Grid(GeoCoordinate a, GeoCoordinate b)
    {
        var km = Math.Sqrt(Math.Pow(a.Latitude - b.Latitude, 2) + Math.Pow(a.Longitude - b.Longitude, 2));
        return (km, TimeSpan.FromHours(km / 60));
    }

    private static readonly DateTimeOffset Start = new(2026, 10, 29, 6, 0, 0, TimeSpan.Zero);

    private static RunStop Stop(string id, double x, double y, DateTimeOffset? earliest = null, DateTimeOffset? latest = null) =>
        new(id, new GeoCoordinate(x, y), earliest, latest, TimeSpan.FromMinutes(10));

    [Fact]
    public void Two_opt_removes_crossing()
    {
        // corners of a square, given in a crossing order
        var stops = new[] { Stop("A", 0, 10), Stop("C", 10, 0), Stop("B", 10, 10), Stop("D", 0, 1) };
        var plan = new RunOptimizer(Grid).Optimize(new GeoCoordinate(0, 0), Start, stops);

        Assert.Equal(4, plan.Stops.Count);
        Assert.InRange(plan.DistanceKm, 39.9, 40.1);
        Assert.Equal(0, plan.LateStops);
    }

    [Fact]
    public void Time_windows_override_distance()
    {
        // B is far but must be served first
        var stops = new[] { Stop("A", 0, 5), Stop("B", 0, 50, latest: Start.AddMinutes(50)) };
        var plan = new RunOptimizer(Grid).Optimize(new GeoCoordinate(0, 0), Start, stops, returnToDepot: false);

        Assert.Equal("B", plan.Stops[0].Stop.Id);
        Assert.Equal(0, plan.LateStops);
    }

    [Fact]
    public void Waits_for_earliest_time()
    {
        var stops = new[] { Stop("A", 0, 6, earliest: Start.AddHours(2)) };
        var plan = new RunOptimizer(Grid).Optimize(new GeoCoordinate(0, 0), Start, stops, returnToDepot: false);
        Assert.Equal(Start.AddHours(2), plan.Stops[0].Arrival);
    }

    [Fact]
    public void Two_opt_swap_reverses_segment() =>
        Assert.Equal(new[] { 1, 4, 3, 2, 5 }, RunOptimizer.TwoOptSwap(new List<int> { 1, 2, 3, 4, 5 }, 1, 3));
}

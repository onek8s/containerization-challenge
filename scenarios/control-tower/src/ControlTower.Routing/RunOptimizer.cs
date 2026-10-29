namespace Proseware.ControlTower.Routing;

public sealed record RunStop(string Id, GeoCoordinate Position, DateTimeOffset? Earliest, DateTimeOffset? Latest, TimeSpan ServiceTime);

public sealed record PlannedStop(RunStop Stop, DateTimeOffset Arrival, DateTimeOffset Departure, bool Late);

public sealed record RunPlan(IReadOnlyList<PlannedStop> Stops, double DistanceKm, TimeSpan Duration, int LateStops);

/// <summary>
/// Orders the stops of a pickup / delivery run: nearest neighbour start, then 2-opt improvement on
/// distance with a heavy penalty for missed time windows.
/// </summary>
public sealed class RunOptimizer
{
    private readonly Func<GeoCoordinate, GeoCoordinate, (double Km, TimeSpan Time)> _travel;

    public RunOptimizer(Func<GeoCoordinate, GeoCoordinate, (double Km, TimeSpan Time)> travel) => _travel = travel;

    public RunOptimizer(IRoutePlanner planner)
        : this((a, b) =>
        {
            var p = planner.Plan(a, b, Domain.TransportMode.Road);
            return (p.DistanceKm, p.Duration);
        })
    {
    }

    /// <summary>Penalty in km-equivalent per hour late.</summary>
    public double LatenessPenaltyPerHour { get; init; } = 500;

    public int MaxIterations { get; init; } = 200;

    public RunPlan Optimize(GeoCoordinate depot, DateTimeOffset start, IReadOnlyList<RunStop> stops, bool returnToDepot = true)
    {
        if (stops.Count == 0) return new RunPlan(Array.Empty<PlannedStop>(), 0, TimeSpan.Zero, 0);

        // matrix: index 0 = depot
        var points = new List<GeoCoordinate> { depot };
        points.AddRange(stops.Select(s => s.Position));
        var n = points.Count;
        var km = new double[n, n];
        var time = new TimeSpan[n, n];
        for (var i = 0; i < n; i++)
        for (var j = 0; j < n; j++)
        {
            if (i == j) continue;
            var t = _travel(points[i], points[j]);
            km[i, j] = t.Km;
            time[i, j] = t.Time;
        }

        var order = NearestNeighbour(km, n);
        var best = Cost(order, km, time, stops, start, returnToDepot);
        var improved = true;
        var iterations = 0;
        while (improved && iterations++ < MaxIterations)
        {
            improved = false;
            for (var i = 0; i < order.Count - 1; i++)
            {
                for (var k = i + 1; k < order.Count; k++)
                {
                    var candidate = TwoOptSwap(order, i, k);
                    var cost = Cost(candidate, km, time, stops, start, returnToDepot);
                    if (cost < best - 1e-9)
                    {
                        order = candidate;
                        best = cost;
                        improved = true;
                    }
                }
            }
        }
        return Schedule(order, km, time, stops, start, returnToDepot);
    }

    internal static List<int> NearestNeighbour(double[,] km, int n)
    {
        var order = new List<int>();
        var visited = new bool[n];
        visited[0] = true;
        var current = 0;
        for (var step = 1; step < n; step++)
        {
            var next = -1;
            var bestKm = double.MaxValue;
            for (var j = 1; j < n; j++)
            {
                if (visited[j] || km[current, j] >= bestKm) continue;
                bestKm = km[current, j];
                next = j;
            }
            visited[next] = true;
            order.Add(next);
            current = next;
        }
        return order;
    }

    internal static List<int> TwoOptSwap(List<int> order, int i, int k)
    {
        var result = new List<int>(order.Count);
        result.AddRange(order.Take(i));
        result.AddRange(order.Skip(i).Take(k - i + 1).Reverse());
        result.AddRange(order.Skip(k + 1));
        return result;
    }

    private double Cost(List<int> order, double[,] km, TimeSpan[,] time, IReadOnlyList<RunStop> stops, DateTimeOffset start, bool returnToDepot)
    {
        var plan = Schedule(order, km, time, stops, start, returnToDepot);
        var lateHours = plan.Stops.Where(s => s.Late && s.Stop.Latest is not null).Sum(s => (s.Arrival - s.Stop.Latest!.Value).TotalHours);
        return plan.DistanceKm + lateHours * LatenessPenaltyPerHour + plan.LateStops * 50;
    }

    private static RunPlan Schedule(List<int> order, double[,] km, TimeSpan[,] time, IReadOnlyList<RunStop> stops, DateTimeOffset start, bool returnToDepot)
    {
        var result = new List<PlannedStop>();
        var clock = start;
        var prev = 0;
        double distance = 0;
        foreach (var idx in order)
        {
            var stop = stops[idx - 1];
            clock += time[prev, idx];
            distance += km[prev, idx];
            if (stop.Earliest is { } earliest && clock < earliest) clock = earliest;
            var late = stop.Latest is { } latest && clock > latest;
            var arrival = clock;
            clock += stop.ServiceTime;
            result.Add(new PlannedStop(stop, arrival, clock, late));
            prev = idx;
        }
        if (returnToDepot)
        {
            clock += time[prev, 0];
            distance += km[prev, 0];
        }
        return new RunPlan(result, Math.Round(distance, 1), clock - start, result.Count(s => s.Late));
    }
}

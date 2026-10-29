using System.Collections.Concurrent;

namespace Proseware.ControlTower.Events;

/// <summary>Tracks since when each vehicle has been standing still, from the live position stream.</summary>
public sealed class VehicleMotionState
{
    private readonly ConcurrentDictionary<int, DateTimeOffset?> _stoppedSince = new();

    public void Update(int vehicleId, int speedKmh, DateTimeOffset at, int stoppedBelowKmh)
    {
        if (speedKmh < stoppedBelowKmh)
            _stoppedSince.AddOrUpdate(vehicleId, at, (_, existing) => existing ?? at);
        else
            _stoppedSince[vehicleId] = null;
    }

    public IReadOnlyDictionary<int, DateTimeOffset?> Snapshot() => new Dictionary<int, DateTimeOffset?>(_stoppedSince);
}

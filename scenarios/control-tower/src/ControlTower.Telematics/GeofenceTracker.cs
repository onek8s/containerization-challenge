using Proseware.ControlTower.Domain.Locations;

namespace Proseware.ControlTower.Telematics;

public sealed record GeofenceTransition(int VehicleId, Location Location, bool Entered);

/// <summary>Remembers which geofences each vehicle is inside and reports enter / leave transitions.</summary>
public sealed class GeofenceTracker
{
    private readonly Dictionary<int, HashSet<int>> _inside = new();
    private IReadOnlyList<Location> _locations = Array.Empty<Location>();
    private readonly int _exitMargin;

    public GeofenceTracker(int exitMarginMeters) => _exitMargin = exitMarginMeters;

    public void SetLocations(IReadOnlyList<Location> locations) => _locations = locations;

    public IReadOnlyList<GeofenceTransition> Update(int vehicleId, double latitude, double longitude)
    {
        if (!_inside.TryGetValue(vehicleId, out var current))
        {
            current = new HashSet<int>();
            _inside[vehicleId] = current;
        }

        var result = new List<GeofenceTransition>();
        foreach (var location in _locations)
        {
            var distance = GeoPoints.DistanceMeters(latitude, longitude, location.Position.Y, location.Position.X);
            var wasInside = current.Contains(location.Id);
            if (!wasInside && distance <= location.GeofenceRadiusMeters)
            {
                current.Add(location.Id);
                result.Add(new GeofenceTransition(vehicleId, location, true));
            }
            else if (wasInside && distance > location.GeofenceRadiusMeters + _exitMargin)
            {
                current.Remove(location.Id);
                result.Add(new GeofenceTransition(vehicleId, location, false));
            }
        }
        return result;
    }

    public bool IsInsideAny(int vehicleId) => _inside.TryGetValue(vehicleId, out var set) && set.Count > 0;
}

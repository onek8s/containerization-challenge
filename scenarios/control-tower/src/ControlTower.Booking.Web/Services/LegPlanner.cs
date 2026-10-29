using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Locations;
using Proseware.ControlTower.Domain.Parties;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Booking.Services;

/// <summary>
/// Default routing for new bookings. Road: one leg. Sea and air: pre-carriage by road to the nearest port or
/// airport, main carriage, on-carriage by road. Operations adjust legs afterwards when needed.
/// </summary>
public sealed class LegPlanner
{
    private const double RoadKmFactor = 1.25;
    private const double RoadKmPerDay = 650;

    public IReadOnlyList<Leg> Plan(TransportMode mode, Location origin, Location destination, IReadOnlyCollection<Location> network,
        Carrier? mainCarrier, Carrier? roadCarrier, DateTimeOffset pickup, int transitDays)
    {
        var stops = new List<Location> { origin };
        var hubKind = mode switch { TransportMode.Sea => LocationKind.Seaport, TransportMode.Air => LocationKind.Airport, _ => (LocationKind?)null };
        if (hubKind is { } kind)
        {
            var loadPort = origin.Kind == kind ? origin : Nearest(network, kind, origin);
            var dischargePort = destination.Kind == kind ? destination : Nearest(network, kind, destination);
            if (loadPort.Id != origin.Id) stops.Add(loadPort);
            if (dischargePort.Id != destination.Id) stops.Add(dischargePort);
        }
        stops.Add(destination);

        var distances = new List<double>();
        for (var i = 0; i < stops.Count - 1; i++)
            distances.Add(GeoPoints.DistanceMeters(stops[i].Position, stops[i + 1].Position) / 1000 * RoadKmFactor);

        // Main carriage runs between two hubs of the booked mode; everything else is road.
        var modes = new List<TransportMode>();
        for (var i = 0; i < stops.Count - 1; i++)
            modes.Add(hubKind is { } k && stops[i].Kind == k && stops[i + 1].Kind == k ? mode : (hubKind is null ? mode : TransportMode.Road));

        var roadHours = distances.Where((_, i) => modes[i] == TransportMode.Road && mode != TransportMode.Road).Sum(d => Math.Max(4, d / RoadKmPerDay * 24));
        var totalHours = Math.Max(24, transitDays * 24.0);
        var mainDistance = Math.Max(1, distances.Where((_, i) => modes[i] == mode).Sum());

        var legs = new List<Leg>();
        var departure = pickup;
        for (var i = 0; i < stops.Count - 1; i++)
        {
            var legMode = modes[i];
            var hours = legMode == TransportMode.Road && mode != TransportMode.Road
                ? Math.Max(4, distances[i] / RoadKmPerDay * 24)
                : Math.Max(4, (totalHours - roadHours) * distances[i] / mainDistance);
            var arrival = departure.AddHours(Math.Round(hours));
            var main = legMode == mode;
            legs.Add(new Leg
            {
                Sequence = i + 1,
                Mode = legMode,
                FromId = stops[i].Id,
                From = stops[i],
                ToId = stops[i + 1].Id,
                To = stops[i + 1],
                CarrierId = main ? mainCarrier?.Id : roadCarrier?.Id,
                Carrier = main ? mainCarrier : roadCarrier,
                Status = LegStatus.Planned,
                PlannedDeparture = departure,
                PlannedArrival = arrival,
                DistanceKm = Math.Round(distances[i])
            });
            // Cut-off and handling at the hub before the next leg.
            departure = arrival.AddHours(main ? 12 : 6);
        }
        return legs;
    }

    private static Location Nearest(IEnumerable<Location> network, LocationKind kind, Location to) =>
        network.Where(l => l.Kind == kind)
            .OrderBy(l => l.CountryCode == to.CountryCode ? 0 : 1)
            .ThenBy(l => GeoPoints.DistanceMeters(l.Position, to.Position))
            .FirstOrDefault()
        ?? throw new InvalidOperationException($"No {kind} in the network for {to.Code}.");
}

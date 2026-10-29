using Microsoft.Extensions.Options;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Locations;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Routing;

public sealed record LegEta(int LegId, int Sequence, DateTimeOffset Departure, DateTimeOffset Arrival, double DistanceKm);

public sealed record EtaResult(DateTimeOffset Eta, double RemainingKm, IReadOnlyList<LegEta> Legs);

/// <summary>
/// Estimates the arrival of a shipment from the current position of the vehicle on the running leg and
/// the plan of the remaining legs, with driving time rules for road legs and opening hours at the stops.
/// </summary>
public sealed class EtaCalculator
{
    private readonly IRoutePlanner _planner;
    private readonly RoutingOptions _options;

    public EtaCalculator(IRoutePlanner planner, IOptions<RoutingOptions> options)
    {
        _planner = planner;
        _options = options.Value;
    }

    /// <param name="shipment">Shipment with legs and their From/To locations loaded.</param>
    /// <param name="position">Last known position of the vehicle on the running leg, if any.</param>
    public EtaResult? Calculate(Shipment shipment, GeoCoordinate? position, DateTimeOffset now)
    {
        var legs = shipment.Legs.Where(l => l.Status != LegStatus.Cancelled).OrderBy(l => l.Sequence).ToList();
        if (legs.Count == 0 || legs.Any(l => l.From is null || l.To is null)) return null;
        if (legs.All(l => l.Status == LegStatus.Arrived)) return null;

        var result = new List<LegEta>();
        var clock = now;
        double remainingKm = 0;
        var driver = DriverState.Rested;

        foreach (var leg in legs)
        {
            if (leg.Status == LegStatus.Arrived)
            {
                clock = Max(clock, leg.ActualArrival ?? leg.PlannedArrival);
                continue;
            }

            DateTimeOffset departure;
            GeoCoordinate start;
            if (leg.Status == LegStatus.Departed)
            {
                departure = leg.ActualDeparture ?? leg.PlannedDeparture;
                start = position ?? Coordinate(leg.From!);
                clock = now;
                if (leg.Mode == TransportMode.Road) driver = DrivingTime.EstimateFromDeparture(departure, now);
            }
            else
            {
                // next leg starts after handling at the stop, never before its plan
                var ready = result.Count > 0 || legs.Any(l => l.Status == LegStatus.Arrived)
                    ? clock + HandlingTime(leg.From!)
                    : clock;
                departure = Max(ready, leg.PlannedDeparture);
                departure = NextOpening(departure, leg.From!);
                clock = departure;
                start = Coordinate(leg.From!);
                driver = DriverState.Rested;
            }

            var plan = _planner.Plan(start, Coordinate(leg.To!), leg.Mode);
            DateTimeOffset arrival;
            if (leg.Mode == TransportMode.Road)
            {
                (arrival, driver) = DrivingTime.Drive(clock, plan.Duration, driver);
            }
            else if (leg.Status == LegStatus.Departed && leg.Eta is { } carrierEta)
            {
                // sea and air: trust the carrier's ETA when it has one
                arrival = carrierEta;
            }
            else if (leg.Status != LegStatus.Departed && leg.Mode is TransportMode.Sea or TransportMode.Air)
            {
                // scheduled sailing or flight: keep the planned transit, shifted when we are late for it
                var late = departure - leg.PlannedDeparture;
                arrival = late > TimeSpan.Zero ? leg.PlannedArrival + late : leg.PlannedArrival;
            }
            else
            {
                arrival = clock + plan.Duration;
            }

            if (leg.To!.Kind is LocationKind.CustomerSite or LocationKind.Terminal)
                arrival = NextOpening(arrival, leg.To);

            remainingKm += plan.DistanceKm;
            result.Add(new LegEta(leg.Id, leg.Sequence, departure, arrival, plan.DistanceKm));
            clock = arrival;
        }

        return new EtaResult(clock, Math.Round(remainingKm, 1), result);
    }

    public TimeSpan HandlingTime(Location location) => location.Kind switch
    {
        LocationKind.Seaport => TimeSpan.FromHours(_options.PortHandlingHours),
        LocationKind.Airport => TimeSpan.FromHours(_options.AirportHandlingHours),
        LocationKind.Terminal or LocationKind.CarrierDepot => TimeSpan.FromHours(_options.TerminalHandlingHours),
        LocationKind.BorderCrossing => TimeSpan.FromMinutes(30),
        _ => TimeSpan.Zero
    };

    /// <summary>Moves a time outside the opening hours (or at a weekend for customer sites) to the next opening.</summary>
    public static DateTimeOffset NextOpening(DateTimeOffset utc, Location location)
    {
        if (location.Kind is LocationKind.Seaport or LocationKind.Airport or LocationKind.BorderCrossing) return utc;
        var local = LocalTime.ToLocal(utc, location.TimeZoneId);
        var weekendsClosed = location.Kind == LocationKind.CustomerSite;
        for (var i = 0; i < 8; i++)
        {
            var day = local.Date;
            var closedDay = weekendsClosed && day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            if (!closedDay)
            {
                if (local.TimeOfDay < location.OpensAt)
                    return LocalTime.FromLocal(day + location.OpensAt, location.TimeZoneId);
                if (local.TimeOfDay <= location.ClosesAt)
                    return utc;
            }
            local = new DateTimeOffset(day.AddDays(1), local.Offset);
            utc = LocalTime.FromLocal(day.AddDays(1), location.TimeZoneId);
        }
        return utc;
    }

    private static GeoCoordinate Coordinate(Location l) => new(l.Position.Y, l.Position.X);

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
}

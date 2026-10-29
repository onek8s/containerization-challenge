using Microsoft.Extensions.Options;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Routing;
using Proseware.ControlTower.Tests.Integration;
using Xunit;

namespace Proseware.ControlTower.Tests.Routing;

public class EtaCalculatorTests
{
    private static readonly IOptions<RoutingOptions> Options = Microsoft.Extensions.Options.Options.Create(new RoutingOptions());
    private static readonly EtaCalculator Calculator = new(new GreatCircleRoutePlanner(Options), Options);

    [Fact]
    public void Great_circle_planner_applies_detour_and_speed()
    {
        var plan = new GreatCircleRoutePlanner(Options).Plan(new GeoCoordinate(61.4870, 23.7660), new GeoCoordinate(60.3133, 24.9637), TransportMode.Road);
        // ~146 km straight, x1.25
        Assert.InRange(plan.DistanceKm, 175, 190);
        Assert.InRange(plan.Duration.TotalHours, 2.5, 2.9);
        Assert.True(plan.Estimated);
    }

    [Fact]
    public void Eta_from_vehicle_position_on_running_leg_and_next_leg()
    {
        // Thursday 29 Oct 2026, 06:00 UTC = 08:00 in Finland
        var pickup = new DateTimeOffset(2026, 10, 29, 6, 0, 0, TimeSpan.Zero);
        var s = TestData.ReeferShipment(pickup);
        s.Legs[0].Status = LegStatus.Departed;
        s.Legs[0].ActualDeparture = pickup;
        var now = pickup.AddHours(1);
        // halfway between Tampere and Vantaa
        var result = Calculator.Calculate(s, new GeoCoordinate(60.90, 24.36), now);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Legs.Count);
        Assert.InRange((result.Legs[0].Arrival - now).TotalHours, 1, 1.6);
        // second leg leaves at its plan (18:00 UTC), ~ 600 km incl. one break, arrives on Friday
        Assert.Equal(s.Legs[1].PlannedDeparture, result.Legs[1].Departure);
        Assert.True(result.Eta > result.Legs[1].Departure.AddHours(8));
        Assert.True(result.RemainingKm > 600);
    }

    [Fact]
    public void Arrival_at_a_customer_after_hours_moves_to_next_opening()
    {
        var oulu = TestData.Oulu; // 07-16 local, weekdays
        // Friday 30 Oct 2026 20:00 local (18:00 UTC) -> Monday 2 Nov 07:00 local (05:00 UTC, EET)
        var next = EtaCalculator.NextOpening(new DateTimeOffset(2026, 10, 30, 18, 0, 0, TimeSpan.Zero), oulu);
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 5, 0, 0, TimeSpan.Zero), next);

        var during = new DateTimeOffset(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);
        Assert.Equal(during, EtaCalculator.NextOpening(during, oulu));
    }

    [Fact]
    public void Delivered_shipment_has_no_eta()
    {
        var s = TestData.ReeferShipment(DateTimeOffset.UtcNow);
        foreach (var l in s.Legs) l.Status = LegStatus.Arrived;
        Assert.Null(Calculator.Calculate(s, null, DateTimeOffset.UtcNow));
    }
}

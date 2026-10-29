using NRules.Fluent.Dsl;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Tracking;
using Proseware.ControlTower.Rules.Facts;

namespace Proseware.ControlTower.Rules.RuleSets;

[Name("Pickup missed"), Tag("schedule")]
[Description("Booked shipment not picked up two hours after the planned pickup time.")]
public sealed class PickupMissedRule : Rule
{
    public override void Define()
    {
        ClockFact clock = null!;
        ShipmentFact s = null!;

        When()
            .Match(() => clock)
            .Match(() => s,
                x => x.Status == ShipmentStatus.Booked,
                x => x.ActualPickup == null,
                x => x.PlannedPickup.AddHours(2) < clock.Now);

        Then()
            .Do(ctx => ctx.Insert(new AlertCandidate(
                s.ShipmentId,
                AlertRules.PickupMissed,
                clock.Now - s.PlannedPickup > TimeSpan.FromHours(6) ? Severity.Critical : Severity.Warning,
                $"Pickup planned at {s.PlannedPickup:yyyy-MM-dd HH:mm} UTC has not been confirmed ({(clock.Now - s.PlannedPickup).TotalHours:0} h).")));
    }
}

[Name("Delivery late"), Tag("schedule")]
[Description("Shipment not delivered by the promised delivery time.")]
public sealed class DeliveryLateRule : Rule
{
    public override void Define()
    {
        ClockFact clock = null!;
        ShipmentFact s = null!;

        When()
            .Match(() => clock)
            .Match(() => s,
                x => x.IsOpen,
                x => x.PlannedDelivery < clock.Now);

        Then()
            .Do(ctx => ctx.Insert(new AlertCandidate(
                s.ShipmentId,
                AlertRules.DeliveryLate,
                Severity.Critical,
                $"Delivery was promised for {s.PlannedDelivery:yyyy-MM-dd HH:mm} UTC; shipment is {s.Status}.")));
    }
}

[Name("ETA slipped"), Tag("schedule")]
[Description("Estimated arrival is more than two hours after the promised delivery, before the promise has passed.")]
public sealed class EtaSlippedRule : Rule
{
    public override void Define()
    {
        ClockFact clock = null!;
        ShipmentFact s = null!;

        When()
            .Match(() => clock)
            .Match(() => s,
                x => x.IsOpen,
                x => x.PlannedDelivery >= clock.Now,
                x => x.Eta != null && x.Eta.Value > x.PlannedDelivery.AddHours(2));

        Then()
            .Do(ctx => ctx.Insert(new AlertCandidate(
                s.ShipmentId,
                AlertRules.EtaSlipped,
                s.Eta!.Value - s.PlannedDelivery > TimeSpan.FromHours(24) ? Severity.Critical : Severity.Warning,
                $"Estimated arrival {s.Eta:yyyy-MM-dd HH:mm} UTC is {(s.Eta!.Value - s.PlannedDelivery).TotalHours:0} h after the promised delivery.")));
    }
}

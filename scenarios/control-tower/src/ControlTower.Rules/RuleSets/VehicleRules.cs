using NRules.Fluent.Dsl;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Tracking;
using Proseware.ControlTower.Rules.Facts;

namespace Proseware.ControlTower.Rules.RuleSets;

[Name("Temperature excursion"), Tag("cold chain")]
[Description("Reefer reading outside the shipment's temperature range (0.5 °C tolerance).")]
public sealed class TemperatureExcursionRule : Rule
{
    public const decimal Tolerance = 0.5m;

    public override void Define()
    {
        ShipmentFact s = null!;
        VehicleReadingFact v = null!;

        When()
            .Match(() => s, x => x.IsMoving, x => x.IsReefer, x => x.VehicleId != null)
            .Match(() => v,
                x => x.VehicleId == s.VehicleId,
                x => x.Temperature != null,
                x => (s.TemperatureMin != null && x.Temperature!.Value < s.TemperatureMin.Value - Tolerance)
                     || (s.TemperatureMax != null && x.Temperature!.Value > s.TemperatureMax.Value + Tolerance));

        Then()
            .Do(ctx => ctx.Insert(new AlertCandidate(
                s.ShipmentId,
                AlertRules.TemperatureExcursion,
                Severity.Critical,
                $"Reefer of {v.Plate} reads {v.Temperature:0.0} °C, required {s.TemperatureMin:0.#}..{s.TemperatureMax:0.#} °C.")));
    }
}

[Name("Reefer door open"), Tag("cold chain")]
[Description("Reefer door reported open while the vehicle is moving.")]
public sealed class ReeferDoorOpenRule : Rule
{
    public override void Define()
    {
        ShipmentFact s = null!;
        VehicleReadingFact v = null!;

        When()
            .Match(() => s, x => x.IsMoving, x => x.IsReefer, x => x.VehicleId != null)
            .Match(() => v, x => x.VehicleId == s.VehicleId, x => x.DoorOpen == true, x => x.SpeedKmh > 5);

        Then()
            .Do(ctx => ctx.Insert(new AlertCandidate(s.ShipmentId, AlertRules.ReeferDoorOpen, Severity.Critical,
                $"Reefer door of {v.Plate} is open while driving at {v.SpeedKmh} km/h.")));
    }
}

[Name("No tracking signal"), Tag("visibility")]
[Description("No position from the vehicle for 45 minutes while the shipment is on the road.")]
public sealed class NoTrackingSignalRule : Rule
{
    public override void Define()
    {
        ClockFact clock = null!;
        ShipmentFact s = null!;
        VehicleReadingFact v = null!;

        When()
            .Match(() => clock)
            .Match(() => s, x => x.IsMoving, x => x.VehicleId != null, x => x.Mode == TransportMode.Road)
            .Match(() => v, x => x.VehicleId == s.VehicleId, x => x.LastSignalAt == null || x.LastSignalAt.Value.AddMinutes(45) < clock.Now);

        Then()
            .Do(ctx => ctx.Insert(new AlertCandidate(s.ShipmentId, AlertRules.NoTrackingSignal, Severity.Warning,
                v.LastSignalAt == null
                    ? $"No position received from {v.Plate}."
                    : $"Last position from {v.Plate} at {v.LastSignalAt:HH:mm} UTC ({(clock.Now - v.LastSignalAt.Value).TotalMinutes:0} min ago).")));
    }
}

[Name("Long stop"), Tag("visibility")]
[Description("Vehicle standing for more than 90 minutes outside known locations (breaks are 45 min).")]
public sealed class LongStopRule : Rule
{
    public override void Define()
    {
        ClockFact clock = null!;
        ShipmentFact s = null!;
        VehicleReadingFact v = null!;

        When()
            .Match(() => clock)
            .Match(() => s, x => x.IsMoving, x => x.VehicleId != null)
            .Match(() => v,
                x => x.VehicleId == s.VehicleId,
                x => !x.AtKnownLocation,
                x => x.StoppedSince != null && x.StoppedSince.Value.AddMinutes(90) < clock.Now);

        Then()
            .Do(ctx => ctx.Insert(new AlertCandidate(s.ShipmentId, AlertRules.LongStop, Severity.Warning,
                $"{v.Plate} has been standing since {v.StoppedSince:HH:mm} UTC outside a known location.")));
    }
}

[Name("Vehicle overweight"), Tag("safety")]
[Description("Open shipments on a vehicle exceed its payload.")]
public sealed class OverweightRule : Rule
{
    public override void Define()
    {
        VehicleLoadFact load = null!;

        When()
            .Match(() => load, x => x.MaxPayloadKg > 0, x => x.LoadKg > x.MaxPayloadKg);

        Then()
            .Do(ctx => Raise(ctx, load));
    }

    private static void Raise(NRules.RuleModel.IContext ctx, VehicleLoadFact load)
    {
        foreach (var id in load.ShipmentIds)
            ctx.Insert(new AlertCandidate(id, AlertRules.Overweight, Severity.Critical,
                $"{load.Plate} carries {load.LoadKg:0} kg, payload is {load.MaxPayloadKg:0} kg."));
    }
}

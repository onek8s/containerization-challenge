using NRules.Fluent.Dsl;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Customs;
using Proseware.ControlTower.Domain.Tracking;
using Proseware.ControlTower.Rules.Facts;

namespace Proseware.ControlTower.Rules.RuleSets;

[Name("Dangerous goods segregation"), Tag("dangerous goods")]
[Description("Two dangerous goods packages on the same vehicle that may not be loaded together.")]
public sealed class DangerousGoodsSegregationRule : Rule
{
    public override void Define()
    {
        DangerousGoodsFact a = null!;
        DangerousGoodsFact b = null!;

        When()
            .Match(() => a, x => x.VehicleId != null, x => !x.LimitedQuantity)
            .Match(() => b,
                x => x.VehicleId == a.VehicleId,
                x => !x.LimitedQuantity,
                x => string.CompareOrdinal(x.Sscc, a.Sscc) > 0,
                x => DangerousGoodsSegregation.Check(a.HazardClass, x.HazardClass) != SegregationResult.Allowed);

        Then()
            .Do(ctx => Raise(ctx, a, b));
    }

    private static void Raise(NRules.RuleModel.IContext ctx, DangerousGoodsFact a, DangerousGoodsFact b)
    {
        var result = DangerousGoodsSegregation.Check(a.HazardClass, b.HazardClass);
        var severity = result == SegregationResult.Prohibited ? Severity.Critical : Severity.Warning;
        var text = result == SegregationResult.Prohibited ? "may not be loaded together" : "need separate stowage";
        var message = $"UN {a.UnNumber} (class {a.HazardClass}) and UN {b.UnNumber} (class {b.HazardClass}) {text}.";
        ctx.Insert(new AlertCandidate(a.ShipmentId, AlertRules.DangerousGoodsSegregation, severity, message));
        if (b.ShipmentId != a.ShipmentId)
            ctx.Insert(new AlertCandidate(b.ShipmentId, AlertRules.DangerousGoodsSegregation, severity, message));
    }
}

[Name("Dangerous goods with food"), Tag("dangerous goods")]
[Description("Toxic or infectious substances on the same vehicle as foodstuffs (ADR 7.5.4).")]
public sealed class DangerousGoodsWithFoodRule : Rule
{
    public override void Define()
    {
        DangerousGoodsFact dg = null!;
        ShipmentFact food = null!;

        When()
            .Match(() => dg, x => x.VehicleId != null, x => DangerousGoodsSegregation.MustBeKeptFromFood(x.HazardClass, x.UnNumber))
            .Match(() => food, x => x.IsFoodstuff, x => x.VehicleId == dg.VehicleId, x => x.ShipmentId != dg.ShipmentId);

        Then()
            .Do(ctx => ctx.Insert(new AlertCandidate(food.ShipmentId, AlertRules.DangerousGoodsSegregation, Severity.Critical,
                $"Foodstuffs loaded with UN {dg.UnNumber} (class {dg.HazardClass}) of shipment {dg.ShipmentId}.")));
    }
}

[Name("Dangerous goods vehicle"), Tag("dangerous goods")]
[Description("Dangerous goods above limited quantity on a vehicle without ADR equipment.")]
public sealed class DangerousGoodsVehicleRule : Rule
{
    public override void Define()
    {
        DangerousGoodsFact dg = null!;
        VehicleReadingFact v = null!;

        When()
            .Match(() => dg, x => x.VehicleId != null, x => !x.LimitedQuantity)
            .Match(() => v, x => x.VehicleId == dg.VehicleId, x => !x.AdrCertified);

        Then()
            .Do(ctx => ctx.Insert(new AlertCandidate(dg.ShipmentId, AlertRules.DangerousGoodsVehicle, Severity.Critical,
                $"UN {dg.UnNumber} is assigned to {v.Plate}, which is not ADR equipped.")));
    }
}

[Name("Customs not released"), Tag("customs")]
[Description("Border-crossing shipment about to move or moving without a released declaration.")]
public sealed class CustomsNotReleasedRule : Rule
{
    public override void Define()
    {
        ClockFact clock = null!;
        ShipmentFact s = null!;
        CustomsFact c = null!;

        When()
            .Match(() => clock)
            .Match(() => s, x => x.IsOpen, x => x.CrossesCustomsBorder, x => x.PlannedPickup.AddHours(-4) < clock.Now)
            .Match(() => c,
                x => x.ShipmentId == s.ShipmentId,
                x => x.Status != DeclarationStatus.Released && x.Status != DeclarationStatus.Rejected && x.Status != DeclarationStatus.Cancelled);

        Then()
            .Do(ctx => ctx.Insert(new AlertCandidate(s.ShipmentId, AlertRules.CustomsNotReleased,
                c.Status == DeclarationStatus.UnderControl || s.IsMoving ? Severity.Critical : Severity.Warning,
                c.Status == DeclarationStatus.Draft
                    ? $"{c.Type} declaration has not been submitted; pickup {s.PlannedPickup:yyyy-MM-dd HH:mm} UTC."
                    : $"{c.Type} declaration is {c.Status} since {c.SubmittedAt:yyyy-MM-dd HH:mm} UTC.")));
    }
}

[Name("Customs missing"), Tag("customs")]
[Description("Border-crossing shipment without any declaration close to pickup.")]
public sealed class CustomsMissingRule : Rule
{
    public override void Define()
    {
        ClockFact clock = null!;
        ShipmentFact s = null!;

        When()
            .Match(() => clock)
            .Match(() => s, x => x.IsOpen, x => x.CrossesCustomsBorder, x => x.PlannedPickup.AddHours(-24) < clock.Now)
            .Not<CustomsFact>(c => c.ShipmentId == s.ShipmentId);

        Then()
            .Do(ctx => ctx.Insert(new AlertCandidate(s.ShipmentId, AlertRules.CustomsNotReleased, Severity.Warning,
                "Shipment crosses the customs border but has no declaration.")));
    }
}

[Name("Customs rejected"), Tag("customs")]
public sealed class CustomsRejectedRule : Rule
{
    public override void Define()
    {
        ShipmentFact s = null!;
        CustomsFact c = null!;

        When()
            .Match(() => s, x => x.IsOpen)
            .Match(() => c, x => x.ShipmentId == s.ShipmentId, x => x.Status == DeclarationStatus.Rejected);

        Then()
            .Do(ctx => ctx.Insert(new AlertCandidate(s.ShipmentId, AlertRules.CustomsRejected, Severity.Critical,
                $"{c.Type} declaration rejected: {c.RejectionReason ?? "no reason given"}.")));
    }
}

[Name("Scan off route"), Tag("terminal")]
[Description("Package scanned at a terminal that is not on the shipment's route.")]
public sealed class ScanMismatchRule : Rule
{
    public override void Define()
    {
        ShipmentFact s = null!;
        ScanFact scan = null!;

        When()
            .Match(() => s, x => x.IsOpen)
            .Match(() => scan, x => x.ShipmentId == s.ShipmentId, x => !s.RouteLocationIds.Contains(x.TerminalId));

        Then()
            .Do(ctx => ctx.Insert(new AlertCandidate(s.ShipmentId, AlertRules.ScanMismatch, Severity.Warning,
                $"Package {scan.Sscc} scanned ({scan.Kind}) at {scan.TerminalCode}, which is not on the route.")));
    }
}

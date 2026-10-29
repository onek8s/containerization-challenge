using Microsoft.Extensions.Logging.Abstractions;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Customs;
using Proseware.ControlTower.Domain.Tracking;
using Proseware.ControlTower.Rules;
using Proseware.ControlTower.Rules.Facts;
using Xunit;

namespace Proseware.ControlTower.Tests.Rules;

public class AlertEvaluatorTests
{
    private static readonly AlertEvaluator Evaluator = new(NullLogger<AlertEvaluator>.Instance);
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private static ShipmentFact Shipment(int id = 1, ShipmentStatus status = ShipmentStatus.InTransit, int? vehicle = 7) => new()
    {
        ShipmentId = id,
        Number = $"PF26-{id:D7}",
        Status = status,
        Mode = TransportMode.Road,
        PlannedPickup = Now.AddHours(-5),
        ActualPickup = status >= ShipmentStatus.PickedUp ? Now.AddHours(-5) : null,
        PlannedDelivery = Now.AddHours(10),
        VehicleId = vehicle,
        GrossWeightKg = 1000,
        RouteLocationIds = new[] { 1, 2 }
    };

    private static VehicleReadingFact Vehicle(int id = 7) => new()
    {
        VehicleId = id,
        Plate = "KTP-412",
        LastSignalAt = Now.AddMinutes(-2),
        SpeedKmh = 80,
        AdrCertified = true,
        MaxPayloadKg = 24000
    };

    private static IReadOnlyList<AlertCandidate> Run(Action<AlertFacts> setup)
    {
        var facts = new AlertFacts { Now = Now };
        setup(facts);
        return Evaluator.Evaluate(facts);
    }

    [Fact]
    public void All_rule_sets_are_loaded()
    {
        Assert.True(Evaluator.RuleNames.Count >= 14);
        Assert.Contains("Temperature excursion", Evaluator.RuleNames);
    }

    [Fact]
    public void Healthy_shipment_raises_nothing()
    {
        var result = Run(f =>
        {
            f.Shipments.Add(Shipment());
            f.Vehicles.Add(Vehicle());
        });
        Assert.Empty(result);
    }

    [Fact]
    public void Pickup_missed_escalates_after_six_hours()
    {
        var result = Run(f =>
        {
            f.Shipments.Add(Shipment(1, ShipmentStatus.Booked, null) with { PlannedPickup = Now.AddHours(-3) });
            f.Shipments.Add(Shipment(2, ShipmentStatus.Booked, null) with { PlannedPickup = Now.AddHours(-7) });
            f.Shipments.Add(Shipment(3, ShipmentStatus.Booked, null) with { PlannedPickup = Now.AddHours(-1) });
        });
        Assert.Equal(Severity.Warning, result.Single(a => a.ShipmentId == 1).Severity);
        Assert.Equal(Severity.Critical, result.Single(a => a.ShipmentId == 2).Severity);
        Assert.DoesNotContain(result, a => a.ShipmentId == 3);
    }

    [Fact]
    public void Late_delivery_and_slipped_eta()
    {
        var result = Run(f =>
        {
            f.Shipments.Add(Shipment(1) with { PlannedDelivery = Now.AddHours(-1) });
            f.Shipments.Add(Shipment(2, vehicle: null) with { Eta = Now.AddHours(13) });
            f.Shipments.Add(Shipment(3, vehicle: null) with { Eta = Now.AddHours(11) });
        });
        Assert.Contains(result, a => a.ShipmentId == 1 && a.RuleCode == AlertRules.DeliveryLate && a.Severity == Severity.Critical);
        Assert.Contains(result, a => a.ShipmentId == 2 && a.RuleCode == AlertRules.EtaSlipped && a.Severity == Severity.Warning);
        Assert.DoesNotContain(result, a => a.ShipmentId == 3);
    }

    [Theory]
    [InlineData(5.0, false)]
    [InlineData(8.4, false)]
    [InlineData(8.6, true)]
    [InlineData(1.2, true)]
    public void Reefer_temperature_outside_range_with_tolerance(double temperature, bool expected)
    {
        var result = Run(f =>
        {
            f.Shipments.Add(Shipment() with { TemperatureMin = 2, TemperatureMax = 8 });
            f.Vehicles.Add(Vehicle() with { Temperature = (decimal)temperature });
        });
        Assert.Equal(expected, result.Any(a => a.RuleCode == AlertRules.TemperatureExcursion));
    }

    [Fact]
    public void Door_open_while_driving()
    {
        var result = Run(f =>
        {
            f.Shipments.Add(Shipment() with { TemperatureMin = 2, TemperatureMax = 8 });
            f.Vehicles.Add(Vehicle() with { DoorOpen = true, Temperature = 5 });
        });
        Assert.Single(result, a => a.RuleCode == AlertRules.ReeferDoorOpen);
    }

    [Fact]
    public void Lost_signal_and_long_stop()
    {
        var result = Run(f =>
        {
            f.Shipments.Add(Shipment());
            f.Vehicles.Add(Vehicle() with { LastSignalAt = Now.AddHours(-1), SpeedKmh = 0, StoppedSince = Now.AddHours(-2) });
        });
        Assert.Contains(result, a => a.RuleCode == AlertRules.NoTrackingSignal);
        Assert.Contains(result, a => a.RuleCode == AlertRules.LongStop);
    }

    [Fact]
    public void Stop_at_a_known_location_is_fine()
    {
        var result = Run(f =>
        {
            f.Shipments.Add(Shipment());
            f.Vehicles.Add(Vehicle() with { SpeedKmh = 0, StoppedSince = Now.AddHours(-3), AtKnownLocation = true });
        });
        Assert.DoesNotContain(result, a => a.RuleCode == AlertRules.LongStop);
    }

    [Fact]
    public void Dangerous_goods_segregation_and_vehicle()
    {
        var result = Run(f =>
        {
            f.Shipments.Add(Shipment(1));
            f.Shipments.Add(Shipment(2));
            f.Vehicles.Add(Vehicle() with { AdrCertified = false });
            f.DangerousGoods.Add(new DangerousGoodsFact { ShipmentId = 1, VehicleId = 7, Sscc = "1", UnNumber = "0336", HazardClass = "1.4G" });
            f.DangerousGoods.Add(new DangerousGoodsFact { ShipmentId = 2, VehicleId = 7, Sscc = "2", UnNumber = "1263", HazardClass = "3" });
        });
        Assert.Equal(2, result.Count(a => a.RuleCode == AlertRules.DangerousGoodsSegregation));
        Assert.Equal(2, result.Count(a => a.RuleCode == AlertRules.DangerousGoodsVehicle));
    }

    [Fact]
    public void Limited_quantities_are_exempt()
    {
        var result = Run(f =>
        {
            f.Shipments.Add(Shipment(1));
            f.Vehicles.Add(Vehicle() with { AdrCertified = false });
            f.DangerousGoods.Add(new DangerousGoodsFact { ShipmentId = 1, VehicleId = 7, Sscc = "1", UnNumber = "3481", HazardClass = "9", LimitedQuantity = true });
        });
        Assert.Empty(result);
    }

    [Fact]
    public void Toxic_goods_with_food()
    {
        var result = Run(f =>
        {
            f.Shipments.Add(Shipment(1) with { IsFoodstuff = true });
            f.Shipments.Add(Shipment(2));
            f.Vehicles.Add(Vehicle());
            f.DangerousGoods.Add(new DangerousGoodsFact { ShipmentId = 2, VehicleId = 7, Sscc = "2", UnNumber = "2810", HazardClass = "6.1" });
        });
        Assert.Contains(result, a => a.ShipmentId == 1 && a.RuleCode == AlertRules.DangerousGoodsSegregation);
    }

    [Fact]
    public void Customs_declaration_states()
    {
        var result = Run(f =>
        {
            f.Shipments.Add(Shipment(1) with { CrossesCustomsBorder = true });
            f.Shipments.Add(Shipment(2) with { CrossesCustomsBorder = true });
            f.Shipments.Add(Shipment(3) with { CrossesCustomsBorder = true });
            f.Shipments.Add(Shipment(4) with { CrossesCustomsBorder = true });
            f.Customs.Add(new CustomsFact { ShipmentId = 1, Status = DeclarationStatus.Draft, Type = DeclarationType.Export });
            f.Customs.Add(new CustomsFact { ShipmentId = 2, Status = DeclarationStatus.Rejected, Type = DeclarationType.Export, RejectionReason = "bad code" });
            f.Customs.Add(new CustomsFact { ShipmentId = 3, Status = DeclarationStatus.Released, Type = DeclarationType.Export });
        });
        Assert.Contains(result, a => a.ShipmentId == 1 && a.RuleCode == AlertRules.CustomsNotReleased && a.Severity == Severity.Critical);
        Assert.Contains(result, a => a.ShipmentId == 2 && a.RuleCode == AlertRules.CustomsRejected);
        Assert.DoesNotContain(result, a => a.ShipmentId == 3);
        Assert.Contains(result, a => a.ShipmentId == 4 && a.RuleCode == AlertRules.CustomsNotReleased && a.Message.Contains("no declaration"));
    }

    [Fact]
    public void Scan_off_route_and_overweight()
    {
        var result = Run(f =>
        {
            f.Shipments.Add(Shipment(1) with { GrossWeightKg = 15000 });
            f.Shipments.Add(Shipment(2) with { GrossWeightKg = 10000 });
            f.Vehicles.Add(Vehicle());
            f.Scans.Add(new ScanFact { ShipmentId = 1, Sscc = "364300123450003008", TerminalId = 9, TerminalCode = "SEGOT", Kind = Proseware.ControlTower.Domain.Scanning.ScanKind.Receive, ScannedAt = Now });
        });
        Assert.Contains(result, a => a.ShipmentId == 1 && a.RuleCode == AlertRules.ScanMismatch);
        Assert.Equal(2, result.Count(a => a.RuleCode == AlertRules.Overweight));
    }
}

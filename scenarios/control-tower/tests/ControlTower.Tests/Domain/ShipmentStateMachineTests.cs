using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Tests.Integration;
using Xunit;

namespace Proseware.ControlTower.Tests.Domain;

public class ShipmentStateMachineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 29, 6, 0, 0, TimeSpan.Zero);

    private static Milestone M(string code, DateTimeOffset at, string source = "test") => new() { Code = code, OccurredAt = at, RecordedAt = at, Source = source };

    [Fact]
    public void Milestones_move_the_status_forward()
    {
        var s = TestData.ReeferShipment(T0);
        Assert.Equal(MilestoneResult.StatusChanged, ShipmentStateMachine.Apply(s, M(MilestoneCodes.PickedUp, T0)));
        Assert.Equal(T0, s.ActualPickup);
        Assert.Equal(MilestoneResult.StatusChanged, ShipmentStateMachine.Apply(s, M(MilestoneCodes.ArrivedAtTerminal, T0.AddHours(4))));
        Assert.Equal(MilestoneResult.StatusChanged, ShipmentStateMachine.Apply(s, M(MilestoneCodes.DepartedTerminal, T0.AddHours(12))));
        Assert.Equal(ShipmentStatus.InTransit, s.Status);
        Assert.Equal(MilestoneResult.StatusChanged, ShipmentStateMachine.Apply(s, M(MilestoneCodes.Delivered, T0.AddDays(1))));
        Assert.Equal(ShipmentStatus.Delivered, s.Status);
        Assert.Equal(T0.AddDays(1), s.ActualDelivery);
    }

    [Fact]
    public void Late_milestone_is_stored_without_moving_status_back()
    {
        var s = TestData.ReeferShipment(T0);
        ShipmentStateMachine.Apply(s, M(MilestoneCodes.PickedUp, T0));
        ShipmentStateMachine.Apply(s, M(MilestoneCodes.OutForDelivery, T0.AddHours(20)));

        var result = ShipmentStateMachine.Apply(s, M(MilestoneCodes.ArrivedAtTerminal, T0.AddHours(4)));

        Assert.Equal(MilestoneResult.RecordedOutOfOrder, result);
        Assert.Equal(ShipmentStatus.OutForDelivery, s.Status);
        Assert.Equal(3, s.Milestones.Count);
    }

    [Fact]
    public void Duplicates_are_ignored()
    {
        var s = TestData.ReeferShipment(T0);
        ShipmentStateMachine.Apply(s, M(MilestoneCodes.PickedUp, T0, "edi:1"));
        Assert.Equal(MilestoneResult.Duplicate, ShipmentStateMachine.Apply(s, M(MilestoneCodes.PickedUp, T0, "edi:1")));
        Assert.Single(s.Milestones);
    }

    [Fact]
    public void Delivered_shipment_cannot_be_cancelled()
    {
        Assert.False(ShipmentStateMachine.CanTransition(ShipmentStatus.Delivered, ShipmentStatus.Cancelled));
        Assert.False(ShipmentStateMachine.CanTransition(ShipmentStatus.InTransit, ShipmentStatus.Cancelled));
        Assert.True(ShipmentStateMachine.CanTransition(ShipmentStatus.Booked, ShipmentStatus.Cancelled));
        Assert.True(ShipmentStateMachine.CanTransition(ShipmentStatus.CustomsHold, ShipmentStatus.AtTerminal));
        Assert.True(ShipmentStateMachine.CanTransition(ShipmentStatus.AtTerminal, ShipmentStatus.InTransit));
    }

    [Fact]
    public void Customs_hold_and_release()
    {
        var s = TestData.ReeferShipment(T0);
        ShipmentStateMachine.Apply(s, M(MilestoneCodes.PickedUp, T0));
        ShipmentStateMachine.Apply(s, M(MilestoneCodes.CustomsHeld, T0.AddHours(2)));
        Assert.Equal(ShipmentStatus.CustomsHold, s.Status);
        ShipmentStateMachine.Apply(s, M(MilestoneCodes.CustomsReleased, T0.AddHours(5)));
        Assert.Equal(ShipmentStatus.AtTerminal, s.Status);
    }
}

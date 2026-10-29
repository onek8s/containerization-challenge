namespace Proseware.ControlTower.Domain.Shipments;

/// <summary>Moves a shipment's status when milestones are recorded.</summary>
public static class ShipmentStateMachine
{
    private static readonly Dictionary<string, ShipmentStatus> StatusByMilestone = new(StringComparer.Ordinal)
    {
        [MilestoneCodes.Booked] = ShipmentStatus.Booked,
        [MilestoneCodes.PickedUp] = ShipmentStatus.PickedUp,
        [MilestoneCodes.ArrivedAtTerminal] = ShipmentStatus.AtTerminal,
        [MilestoneCodes.DepartedTerminal] = ShipmentStatus.InTransit,
        [MilestoneCodes.Loaded] = ShipmentStatus.InTransit,
        [MilestoneCodes.Departed] = ShipmentStatus.InTransit,
        [MilestoneCodes.Arrived] = ShipmentStatus.AtTerminal,
        [MilestoneCodes.Discharged] = ShipmentStatus.AtTerminal,
        [MilestoneCodes.CustomsHeld] = ShipmentStatus.CustomsHold,
        [MilestoneCodes.CustomsReleased] = ShipmentStatus.AtTerminal,
        [MilestoneCodes.OutForDelivery] = ShipmentStatus.OutForDelivery,
        [MilestoneCodes.Delivered] = ShipmentStatus.Delivered,
        [MilestoneCodes.Cancelled] = ShipmentStatus.Cancelled,
    };

    public static bool CanTransition(ShipmentStatus from, ShipmentStatus to)
    {
        if (from == to) return true;
        if (from is ShipmentStatus.Delivered or ShipmentStatus.Cancelled) return false;
        if (to == ShipmentStatus.Cancelled) return from is ShipmentStatus.Requested or ShipmentStatus.Booked;
        if (to == ShipmentStatus.Requested) return false;
        // Customs hold may happen anywhere after pickup and returns to the terminal status when released.
        if (from == ShipmentStatus.CustomsHold) return to is ShipmentStatus.AtTerminal or ShipmentStatus.InTransit or ShipmentStatus.OutForDelivery or ShipmentStatus.Delivered;
        if (to == ShipmentStatus.CustomsHold) return from >= ShipmentStatus.PickedUp;
        // Multi-leg shipments go back and forth between in transit and at terminal.
        if (from is ShipmentStatus.InTransit or ShipmentStatus.AtTerminal && to is ShipmentStatus.InTransit or ShipmentStatus.AtTerminal) return true;
        return to > from;
    }

    /// <summary>
    /// Applies a milestone: adds it, updates actual times and status. Milestones that arrive late
    /// (out of order) are stored but do not move the status backwards.
    /// </summary>
    public static MilestoneResult Apply(Shipment shipment, Milestone milestone)
    {
        if (shipment.Milestones.Any(m => m.Code == milestone.Code && m.OccurredAt == milestone.OccurredAt && m.Source == milestone.Source))
            return MilestoneResult.Duplicate;

        shipment.Milestones.Add(milestone);
        milestone.ShipmentId = shipment.Id;

        if (milestone.Code == MilestoneCodes.PickedUp && shipment.ActualPickup is null)
            shipment.ActualPickup = milestone.OccurredAt;
        if (milestone.Code == MilestoneCodes.Delivered)
            shipment.ActualDelivery = milestone.OccurredAt;

        if (!StatusByMilestone.TryGetValue(milestone.Code, out var target))
            return MilestoneResult.Recorded;

        var latest = shipment.Milestones.Where(m => StatusByMilestone.ContainsKey(m.Code)).MaxBy(m => m.OccurredAt);
        if (!ReferenceEquals(latest, milestone) && target != ShipmentStatus.Delivered)
            return MilestoneResult.RecordedOutOfOrder;

        if (!CanTransition(shipment.Status, target))
            return MilestoneResult.RejectedTransition;

        var changed = shipment.Status != target;
        shipment.Status = target;
        shipment.UpdatedAt = milestone.RecordedAt;
        return changed ? MilestoneResult.StatusChanged : MilestoneResult.Recorded;
    }
}

public enum MilestoneResult
{
    Recorded,
    StatusChanged,
    RecordedOutOfOrder,
    RejectedTransition,
    Duplicate
}

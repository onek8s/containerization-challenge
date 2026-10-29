using Proseware.ControlTower.Domain.Fleet;
using Proseware.ControlTower.Domain.Locations;
using Proseware.ControlTower.Domain.Parties;

namespace Proseware.ControlTower.Domain.Shipments;

public class Leg
{
    public int Id { get; set; }
    public int ShipmentId { get; set; }
    public int Sequence { get; set; }
    public TransportMode Mode { get; set; }
    public int? CarrierId { get; set; }
    public Carrier? Carrier { get; set; }
    public int FromId { get; set; }
    public Location? From { get; set; }
    public int ToId { get; set; }
    public Location? To { get; set; }
    public LegStatus Status { get; set; }
    public DateTimeOffset PlannedDeparture { get; set; }
    public DateTimeOffset PlannedArrival { get; set; }
    public DateTimeOffset? ActualDeparture { get; set; }
    public DateTimeOffset? ActualArrival { get; set; }
    public DateTimeOffset? Eta { get; set; }
    /// <summary>Booking number, B/L or AWB number given by the carrier.</summary>
    public string? CarrierReference { get; set; }
    public string? VesselOrFlight { get; set; }
    public int? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public double? DistanceKm { get; set; }

    public TimeSpan? DelayAgainstPlan(DateTimeOffset now)
    {
        if (ActualArrival is { } arrived) return arrived - PlannedArrival;
        if (Eta is { } eta) return eta - PlannedArrival;
        return Status is LegStatus.Arrived or LegStatus.Cancelled || now <= PlannedArrival ? null : now - PlannedArrival;
    }
}

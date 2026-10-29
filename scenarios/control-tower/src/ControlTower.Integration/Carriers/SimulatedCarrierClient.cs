using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Domain.Parties;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Integration.Carriers;

/// <summary>Carrier API stand-in for test environments: confirms bookings and reports the plan as it passes.</summary>
public sealed class SimulatedCarrierClient : ICarrierApiClient
{
    private readonly Domain.IClock _clock;

    public SimulatedCarrierClient(Domain.IClock clock) => _clock = clock;

    public IReadOnlyCollection<string> CarrierCodes { get; } = Array.Empty<string>();

    public Task<CarrierBookingResult> BookAsync(Carrier carrier, Shipment shipment, Leg leg, CancellationToken cancellationToken)
    {
        var reference = $"SIM-{carrier.Code}-{shipment.Id:D6}{leg.Sequence}";
        return Task.FromResult(new CarrierBookingResult(true, reference, leg.PlannedDeparture, "Simulated booking"));
    }

    public Task<IReadOnlyList<StatusReported>> GetTrackingAsync(Carrier carrier, IReadOnlyList<TrackingQuery> queries, DateTimeOffset since, CancellationToken cancellationToken)
    {
        // Nothing to pull: simulated progress comes from the lab tools (telematics simulator, EDI partner).
        return Task.FromResult<IReadOnlyList<StatusReported>>(Array.Empty<StatusReported>());
    }
}

using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Domain.Parties;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Integration.Carriers;

public sealed record CarrierBookingResult(bool Accepted, string? CarrierReference, DateTimeOffset? ConfirmedPickup, string? Message, string? LabelZpl = null, string? RawResponse = null);

public sealed record TrackingQuery(int ShipmentId, string ShipmentNumber, string CarrierReference);

/// <summary>REST integration of one carrier.</summary>
public interface ICarrierApiClient
{
    /// <summary>Carrier codes this client serves.</summary>
    IReadOnlyCollection<string> CarrierCodes { get; }

    Task<CarrierBookingResult> BookAsync(Carrier carrier, Shipment shipment, Leg leg, CancellationToken cancellationToken);

    Task<IReadOnlyList<StatusReported>> GetTrackingAsync(Carrier carrier, IReadOnlyList<TrackingQuery> queries, DateTimeOffset since, CancellationToken cancellationToken);
}

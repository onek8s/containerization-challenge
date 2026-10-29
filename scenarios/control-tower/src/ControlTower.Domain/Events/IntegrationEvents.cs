namespace Proseware.ControlTower.Domain.Events;

// Messages exchanged between the applications over the message broker.
// Each record maps to its own exchange; names are part of the topology and must not be renamed.

public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}

public sealed record ShipmentBooked(Guid EventId, DateTimeOffset OccurredAt, int ShipmentId, string Number, int CustomerId, TransportMode Mode, string BookedBy) : IIntegrationEvent;

public sealed record ShipmentChanged(Guid EventId, DateTimeOffset OccurredAt, int ShipmentId, string Number, string Change, string ChangedBy) : IIntegrationEvent;

/// <summary>A status event from any source (EDI, carrier API, scan, telematics, user) not yet applied to the shipment.</summary>
public sealed record StatusReported(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string? ShipmentNumber,
    string? CarrierReference,
    string? Sscc,
    string MilestoneCode,
    string? LocationCode,
    string? LocationText,
    string Source,
    string? Remarks) : IIntegrationEvent;

public sealed record MilestoneRecorded(Guid EventId, DateTimeOffset OccurredAt, int ShipmentId, string Number, int CustomerId, string MilestoneCode, string Description, string? Location, ShipmentStatus Status) : IIntegrationEvent;

public sealed record VehiclePositionReported(Guid EventId, DateTimeOffset OccurredAt, int VehicleId, string Plate, double Latitude, double Longitude, int SpeedKmh, int Heading, decimal? Temperature, bool? DoorOpen) : IIntegrationEvent;

public sealed record GeofenceEvent(Guid EventId, DateTimeOffset OccurredAt, int VehicleId, string Plate, int LocationId, string LocationCode, bool Entered) : IIntegrationEvent;

public sealed record EtaUpdated(Guid EventId, DateTimeOffset OccurredAt, int ShipmentId, string Number, int CustomerId, DateTimeOffset? PreviousEta, DateTimeOffset Eta, double RemainingKm) : IIntegrationEvent;

public sealed record AlertRaised(Guid EventId, DateTimeOffset OccurredAt, long AlertId, int ShipmentId, string Number, string RuleCode, Severity Severity, string Message) : IIntegrationEvent;

public sealed record AlertClosed(Guid EventId, DateTimeOffset OccurredAt, long AlertId, int ShipmentId, string ClosedBy, string Resolution) : IIntegrationEvent;

public sealed record ScanRecorded(Guid EventId, DateTimeOffset OccurredAt, long ScanId, string Sscc, int? ShipmentId, Scanning.ScanKind Kind, int TerminalId, string DeviceId) : IIntegrationEvent;

public sealed record CustomsDeclarationRequested(Guid EventId, DateTimeOffset OccurredAt, int DeclarationId, int ShipmentId, string RequestedBy) : IIntegrationEvent;

public sealed record CustomsStatusChanged(Guid EventId, DateTimeOffset OccurredAt, int DeclarationId, int ShipmentId, Customs.DeclarationStatus Status, string? Mrn, string? Reason) : IIntegrationEvent;

public sealed record CarrierBookingRequested(Guid EventId, DateTimeOffset OccurredAt, int ShipmentId, int LegId, int CarrierId, string RequestedBy) : IIntegrationEvent;

public sealed record DocumentRequested(Guid EventId, DateTimeOffset OccurredAt, int ShipmentId, Documents.DocumentKind Kind, string RequestedBy) : IIntegrationEvent;

public sealed record DocumentCreated(Guid EventId, DateTimeOffset OccurredAt, Guid DocumentId, int ShipmentId, Documents.DocumentKind Kind, string FileName) : IIntegrationEvent;

public sealed record LabelPrintRequested(Guid EventId, DateTimeOffset OccurredAt, int ShipmentId, int? PackageId, string PrinterName, string RequestedBy) : IIntegrationEvent;

public static class IntegrationEvent
{
    public static Guid NewId() => Guid.NewGuid();
}

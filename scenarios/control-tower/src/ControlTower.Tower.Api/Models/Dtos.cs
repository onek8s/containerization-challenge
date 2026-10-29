using Proseware.ControlTower.Domain;

namespace Proseware.ControlTower.Tower.Models;

public sealed record CountItem(string Key, int Count);

public sealed record DashboardSummary(
    int OpenShipments,
    IReadOnlyList<CountItem> ByStatus,
    IReadOnlyList<CountItem> ByMode,
    double OnTimeDeliveryPercent7d,
    double OnTimePickupPercent7d,
    int DeliveredToday,
    int PickupsDueToday,
    int LateShipments,
    IReadOnlyList<CountItem> OpenAlertsBySeverity,
    int VehiclesReporting,
    DateTimeOffset GeneratedAt);

public sealed record MapVehicle(
    int Id,
    string Plate,
    string? Carrier,
    double? Latitude,
    double? Longitude,
    DateTimeOffset? LastPositionAt,
    int? SpeedKmh,
    decimal? Temperature,
    bool Reefer,
    bool Stale,
    string? ShipmentNumber,
    bool TemperatureOutOfRange);

public sealed record MapLocation(int Id, string Code, string Name, LocationKind Kind, double Latitude, double Longitude);

public sealed record MapLeg(
    int LegId,
    int ShipmentId,
    string ShipmentNumber,
    TransportMode Mode,
    LegStatus Status,
    string? Carrier,
    string? Vehicle,
    double FromLatitude,
    double FromLongitude,
    string From,
    double ToLatitude,
    double ToLongitude,
    string To,
    DateTimeOffset PlannedArrival,
    DateTimeOffset? Eta,
    bool Late);

public sealed record MapData(IReadOnlyList<MapVehicle> Vehicles, IReadOnlyList<MapLeg> Legs, IReadOnlyList<MapLocation> Locations);

public sealed record AlertRow(
    long Id,
    int ShipmentId,
    string ShipmentNumber,
    string Customer,
    string RuleCode,
    Severity Severity,
    string Message,
    DateTimeOffset RaisedAt,
    DateTimeOffset LastSeenAt,
    int Occurrences,
    string? AcknowledgedBy,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? ResolvedAt,
    string? Resolution);

public sealed record ResolveAlertRequest(string Resolution);

public sealed record KpiPoint(
    DateTime Day,
    string Group,
    int Booked,
    int Delivered,
    double OnTimeDeliveryPercent,
    double OnTimePickupPercent,
    int Alerts,
    double AverageTransitHours,
    decimal Revenue);

public sealed record CarrierScore(
    int CarrierId,
    string Code,
    string Name,
    TransportMode Mode,
    CarrierIntegration Integration,
    int Legs,
    int ArrivedLegs,
    int OnTimeLegs,
    double OnTimePercent,
    decimal TargetPercent,
    double AverageDelayHours,
    int OpenLegs,
    int LateOpenLegs);

public sealed record TerminalHour(DateTimeOffset Hour, int Arrivals, int Departures, int Packages, decimal WeightKg);

public sealed record TerminalWorkload(int TerminalId, string Code, string Name, string TimeZoneId, IReadOnlyList<TerminalHour> Hours, int OnSite);

public sealed record ShipmentDetail(
    int Id,
    string Number,
    string Customer,
    string? CustomerReference,
    TransportMode Mode,
    string Incoterm,
    ShipmentStatus Status,
    string Origin,
    string Destination,
    string Shipper,
    string Consignee,
    DateTimeOffset PlannedPickup,
    DateTimeOffset PlannedDelivery,
    DateTimeOffset? ActualPickup,
    DateTimeOffset? ActualDelivery,
    DateTimeOffset? Eta,
    string Goods,
    decimal GrossWeightKg,
    decimal VolumeM3,
    int Packages,
    bool DangerousGoods,
    decimal? TemperatureMin,
    decimal? TemperatureMax,
    IReadOnlyList<LegDto> Legs,
    IReadOnlyList<MilestoneDto> Milestones,
    IReadOnlyList<PackageDto> PackageList,
    IReadOnlyList<CustomsDto> Customs,
    IReadOnlyList<AlertRow> Alerts);

public sealed record LegDto(int Id, int Sequence, TransportMode Mode, string? Carrier, string From, string To, LegStatus Status,
    DateTimeOffset PlannedDeparture, DateTimeOffset PlannedArrival, DateTimeOffset? ActualDeparture, DateTimeOffset? ActualArrival,
    DateTimeOffset? Eta, string? CarrierReference, string? VesselOrFlight, string? Vehicle, string? TrackingUrl);

public sealed record MilestoneDto(string Code, string Description, DateTimeOffset OccurredAt, string? Location, string Source, string? Remarks);

public sealed record PackageDto(string Sscc, PackageKind Kind, decimal WeightKg, string Dimensions, string? DangerousGoods, string? ContainerNumber);

public sealed record CustomsDto(int Id, string Type, string Status, string Lrn, string? Mrn, DateTimeOffset? SubmittedAt, DateTimeOffset? ReleasedAt, string? RejectionReason);

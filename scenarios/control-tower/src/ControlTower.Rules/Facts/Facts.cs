using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Customs;
using Proseware.ControlTower.Domain.Scanning;

namespace Proseware.ControlTower.Rules.Facts;

/// <summary>Evaluation time; inserted once per session.</summary>
public sealed record ClockFact(DateTimeOffset Now);

public sealed record ShipmentFact
{
    public int ShipmentId { get; init; }
    public string Number { get; init; } = "";
    public ShipmentStatus Status { get; init; }
    public TransportMode Mode { get; init; }
    public DateTimeOffset PlannedPickup { get; init; }
    public DateTimeOffset? ActualPickup { get; init; }
    public DateTimeOffset PlannedDelivery { get; init; }
    public DateTimeOffset? Eta { get; init; }
    public decimal? TemperatureMin { get; init; }
    public decimal? TemperatureMax { get; init; }
    public bool CrossesCustomsBorder { get; init; }
    public decimal GrossWeightKg { get; init; }
    /// <summary>Vehicle of the leg currently under way, if any.</summary>
    public int? VehicleId { get; init; }
    /// <summary>Terminals and stops on the route (location ids).</summary>
    public IReadOnlyCollection<int> RouteLocationIds { get; init; } = Array.Empty<int>();
    /// <summary>Food or feed; must not travel with toxic or infectious substances (ADR 7.5.4).</summary>
    public bool IsFoodstuff { get; init; }

    public bool IsReefer => TemperatureMin.HasValue || TemperatureMax.HasValue;

    public bool IsOpen => Status is not (ShipmentStatus.Delivered or ShipmentStatus.Cancelled or ShipmentStatus.Requested);

    public bool IsMoving => Status is ShipmentStatus.PickedUp or ShipmentStatus.InTransit or ShipmentStatus.OutForDelivery;
}

public sealed record VehicleReadingFact
{
    public int VehicleId { get; init; }
    public string Plate { get; init; } = "";
    public DateTimeOffset? LastSignalAt { get; init; }
    public decimal? Temperature { get; init; }
    public bool? DoorOpen { get; init; }
    public int SpeedKmh { get; init; }
    /// <summary>Since when the vehicle has been standing still (speed 0), if it is.</summary>
    public DateTimeOffset? StoppedSince { get; init; }
    /// <summary>The vehicle stands inside the geofence of a known location (terminal, customer, port).</summary>
    public bool AtKnownLocation { get; init; }
    public bool AdrCertified { get; init; }
    public decimal MaxPayloadKg { get; init; }
}

public sealed record CustomsFact
{
    public int ShipmentId { get; init; }
    public int DeclarationId { get; init; }
    public DeclarationType Type { get; init; }
    public DeclarationStatus Status { get; init; }
    public DateTimeOffset? SubmittedAt { get; init; }
    public string? RejectionReason { get; init; }
}

public sealed record DangerousGoodsFact
{
    public int ShipmentId { get; init; }
    public int? VehicleId { get; init; }
    public string Sscc { get; init; } = "";
    public string UnNumber { get; init; } = "";
    public string HazardClass { get; init; } = "";
    public bool LimitedQuantity { get; init; }
}

public sealed record ScanFact
{
    public int ShipmentId { get; init; }
    public string Sscc { get; init; } = "";
    public ScanKind Kind { get; init; }
    public int TerminalId { get; init; }
    public string TerminalCode { get; init; } = "";
    public DateTimeOffset ScannedAt { get; init; }
}

/// <summary>Sum of the gross weight of all open shipments currently on the vehicle.</summary>
public sealed record VehicleLoadFact(int VehicleId, string Plate, decimal LoadKg, decimal MaxPayloadKg, IReadOnlyCollection<int> ShipmentIds);

/// <summary>Output of the rules: an alert that should be open for the shipment.</summary>
public sealed record AlertCandidate(int ShipmentId, string RuleCode, Severity Severity, string Message);

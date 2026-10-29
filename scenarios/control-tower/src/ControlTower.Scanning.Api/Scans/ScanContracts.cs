using Proseware.ControlTower.Domain.Scanning;

namespace Proseware.ControlTower.Scanning.Scans;

public sealed record ScanDto(
    Guid ClientScanId,
    string Barcode,
    ScanKind Kind,
    string Terminal,
    DateTimeOffset ScannedAt,
    string? DoorOrBay,
    string? VehiclePlate,
    string? UserName,
    string? Remarks);

public sealed record ScanBatch(string DeviceId, IReadOnlyList<ScanDto> Scans);

public enum ScanOutcome
{
    Accepted,
    Duplicate,
    UnknownPackage,
    InvalidBarcode,
    UnknownTerminal
}

public sealed record ScanResult(Guid ClientScanId, ScanOutcome Outcome, long? ScanId, string? ShipmentNumber, string? Message);

public sealed record PackageInfo(
    string Sscc,
    string ShipmentNumber,
    string Status,
    string Consignee,
    string Destination,
    string RouteCode,
    int Index,
    int Count,
    decimal WeightKg,
    bool DangerousGoods,
    bool Reefer,
    string? LastScan,
    DateTimeOffset? LastScanAt);

public sealed record ExpectedPackage(string Sscc, string ShipmentNumber, string Shipper, string Kind, decimal WeightKg, DateTimeOffset PlannedArrival, bool Received);

public sealed record PrintRequest(string Printer, string? ShipmentNumber, string? Sscc);

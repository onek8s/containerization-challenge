using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Documents.Labels;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Domain.Scanning;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Integration.Messaging;

namespace Proseware.ControlTower.Scanning.Scans;

public sealed class ScanService
{
    private readonly ControlTowerDbContext _db;
    private readonly IEventBus _bus;
    private readonly IClock _clock;
    private readonly ILogger<ScanService> _logger;

    public ScanService(ControlTowerDbContext db, IEventBus bus, IClock clock, ILogger<ScanService> logger)
    {
        _db = db;
        _bus = bus;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ScanResult>> RecordAsync(ScanBatch batch, string authenticatedAs, CancellationToken ct)
    {
        var results = new List<ScanResult>(batch.Scans.Count);
        var terminals = await _db.Locations.AsNoTracking()
            .Where(l => l.Kind == LocationKind.Terminal)
            .ToDictionaryAsync(l => l.Code, l => l.Id, StringComparer.OrdinalIgnoreCase, ct);

        var ids = batch.Scans.Select(s => s.ClientScanId).ToList();
        var already = await _db.Scans.AsNoTracking()
            .Where(s => ids.Contains(s.ClientScanId))
            .Select(s => new { s.ClientScanId, s.Id })
            .ToDictionaryAsync(s => s.ClientScanId, s => s.Id, ct);

        var published = new List<ScanRecorded>();
        foreach (var dto in batch.Scans.OrderBy(s => s.ScannedAt))
        {
            if (already.TryGetValue(dto.ClientScanId, out var existingId))
            {
                results.Add(new ScanResult(dto.ClientScanId, ScanOutcome.Duplicate, existingId, null, "Already received."));
                continue;
            }
            var sscc = Sscc.Normalize(dto.Barcode ?? "");
            if (!Sscc.IsValid(sscc))
            {
                results.Add(new ScanResult(dto.ClientScanId, ScanOutcome.InvalidBarcode, null, null, "Not a valid SSCC."));
                continue;
            }
            if (!terminals.TryGetValue(dto.Terminal ?? "", out var terminalId))
            {
                results.Add(new ScanResult(dto.ClientScanId, ScanOutcome.UnknownTerminal, null, null, $"Unknown terminal '{dto.Terminal}'."));
                continue;
            }

            var package = await _db.Packages.AsNoTracking().FirstOrDefaultAsync(p => p.Sscc == sscc, ct);
            string? number = null;
            if (package is not null)
                number = await _db.Shipments.Where(s => s.Id == package.ShipmentId).Select(s => s.Number).FirstOrDefaultAsync(ct);

            var scan = new ScanEvent
            {
                Sscc = sscc,
                PackageId = package?.Id,
                ShipmentId = package?.ShipmentId,
                Kind = dto.Kind,
                TerminalId = terminalId,
                DoorOrBay = dto.DoorOrBay,
                VehiclePlate = dto.VehiclePlate?.ToUpperInvariant(),
                DeviceId = batch.DeviceId,
                UserName = string.IsNullOrWhiteSpace(dto.UserName) ? authenticatedAs : dto.UserName!,
                // Handheld clocks drift; never accept scans from the future.
                ScannedAt = dto.ScannedAt > _clock.UtcNow.AddMinutes(5) ? _clock.UtcNow : dto.ScannedAt,
                ReceivedAt = _clock.UtcNow,
                Remarks = dto.Remarks,
                ClientScanId = dto.ClientScanId
            };
            _db.Scans.Add(scan);
            await _db.SaveChangesAsync(ct);

            published.Add(new ScanRecorded(IntegrationEvent.NewId(), scan.ScannedAt, scan.Id, sscc, scan.ShipmentId, scan.Kind, terminalId, batch.DeviceId));
            results.Add(package is null
                ? new ScanResult(dto.ClientScanId, ScanOutcome.UnknownPackage, scan.Id, null, "Package not booked; scan kept for clearing.")
                : new ScanResult(dto.ClientScanId, ScanOutcome.Accepted, scan.Id, number, null));
        }

        foreach (var evt in published)
            await _bus.PublishAsync(evt, ct);

        _logger.LogInformation("Device {Device}: {Accepted} accepted, {Other} other of {Total} scans",
            batch.DeviceId, results.Count(r => r.Outcome == ScanOutcome.Accepted), results.Count(r => r.Outcome != ScanOutcome.Accepted), results.Count);
        return results;
    }

    public async Task<PackageInfo?> GetPackageAsync(string barcode, CancellationToken ct)
    {
        var sscc = Sscc.Normalize(barcode);
        var package = await _db.Packages.AsNoTracking().FirstOrDefaultAsync(p => p.Sscc == sscc, ct);
        if (package is null) return null;
        var shipment = await _db.Shipments.AsNoTracking()
            .Include(s => s.Destination)
            .Include(s => s.Packages)
            .FirstAsync(s => s.Id == package.ShipmentId, ct);
        var lastScan = await _db.Scans.AsNoTracking().Where(s => s.Sscc == sscc).OrderByDescending(s => s.ScannedAt).FirstOrDefaultAsync(ct);
        var ordered = shipment.Packages.OrderBy(p => p.Id).ToList();
        return new PackageInfo(
            sscc, shipment.Number, shipment.Status.ToString(), shipment.ConsigneeName,
            $"{shipment.Destination!.City}, {shipment.Destination.CountryCode}",
            ZplLabelPrinter.RouteCode(shipment),
            ordered.FindIndex(p => p.Id == package.Id) + 1, ordered.Count,
            package.WeightKg, package.DangerousGoods is not null, shipment.IsReefer,
            lastScan?.Kind.ToString(), lastScan?.ScannedAt);
    }

    /// <summary>Packages on legs arriving at the terminal today (terminal local day).</summary>
    public async Task<IReadOnlyList<ExpectedPackage>?> GetExpectedAsync(string terminalCode, CancellationToken ct)
    {
        var terminal = await _db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Code == terminalCode, ct);
        if (terminal is null) return null;
        var localToday = LocalTime.ToLocal(_clock.UtcNow, terminal.TimeZoneId).Date;
        var from = LocalTime.FromLocal(localToday, terminal.TimeZoneId);
        var to = from.AddDays(1);

        var legs = await _db.Legs.AsNoTracking()
            .Where(l => l.ToId == terminal.Id && l.Status != LegStatus.Cancelled && l.PlannedArrival >= from && l.PlannedArrival < to)
            .Select(l => new { l.ShipmentId, Arrival = l.Eta ?? l.PlannedArrival })
            .ToListAsync(ct);
        var shipmentIds = legs.Select(l => l.ShipmentId).Distinct().ToList();
        var shipments = await _db.Shipments.AsNoTracking().Include(s => s.Packages)
            .Where(s => shipmentIds.Contains(s.Id))
            .ToListAsync(ct);
        var ssccs = shipments.SelectMany(s => s.Packages).Select(p => p.Sscc).ToList();
        var received = (await _db.Scans.AsNoTracking()
                .Where(s => ssccs.Contains(s.Sscc) && s.TerminalId == terminal.Id && (s.Kind == ScanKind.Receive || s.Kind == ScanKind.Unload))
                .Select(s => s.Sscc)
                .ToListAsync(ct))
            .ToHashSet();

        return shipments
            .SelectMany(s => s.Packages.Select(p => new ExpectedPackage(
                p.Sscc, s.Number, s.ShipperName, p.Kind.ToString(), p.WeightKg,
                legs.Where(l => l.ShipmentId == s.Id).Min(l => l.Arrival),
                received.Contains(p.Sscc))))
            .OrderBy(p => p.PlannedArrival).ThenBy(p => p.ShipmentNumber)
            .ToList();
    }
}

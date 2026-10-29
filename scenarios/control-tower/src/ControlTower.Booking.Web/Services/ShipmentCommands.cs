using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Customs;
using Proseware.ControlTower.Domain.Documents;
using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Domain.Parties;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Integration.Messaging;

namespace Proseware.ControlTower.Booking.Services;

/// <summary>Actions on an existing shipment. Most of them are carried out by the back-end services via the bus.</summary>
public sealed class ShipmentCommands
{
    private readonly ControlTowerDbContext _db;
    private readonly IEventBus _bus;
    private readonly IClock _clock;
    private readonly ILogger<ShipmentCommands> _logger;

    public ShipmentCommands(ControlTowerDbContext db, IEventBus bus, IClock clock, ILogger<ShipmentCommands> logger)
    {
        _db = db;
        _bus = bus;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> SubmitCustomsAsync(int shipmentId, int declarationId, string user, CancellationToken ct)
    {
        var declaration = await _db.CustomsDeclarations.Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == declarationId && d.ShipmentId == shipmentId, ct);
        if (declaration is null) return new[] { "Declaration not found." };
        if (declaration.Status is not (DeclarationStatus.Draft or DeclarationStatus.Rejected))
            return new[] { $"Declaration is already {declaration.Status}." };
        var errors = DeclarationValidator.Validate(declaration);
        if (errors.Count > 0) return errors;

        if (declaration.Status == DeclarationStatus.Rejected)
        {
            // A rejected LRN cannot be reused.
            declaration.Lrn = DeclarationValidator.NewLrn(shipmentId, _clock.UtcNow, declaration.SubmitAttempts + 1);
            declaration.RejectionReason = null;
            declaration.Status = DeclarationStatus.Draft;
            await _db.SaveChangesAsync(ct);
        }
        await _bus.PublishAsync(new CustomsDeclarationRequested(IntegrationEvent.NewId(), _clock.UtcNow, declaration.Id, shipmentId, user), ct);
        _logger.LogInformation("{User} sent declaration {Lrn} of shipment {ShipmentId} to customs", user, declaration.Lrn, shipmentId);
        return Array.Empty<string>();
    }

    public async Task UpdateDeclarationLineAsync(int shipmentId, int lineId, string commodityCode, string description, decimal statisticalValue, CancellationToken ct)
    {
        var line = await _db.DeclarationLines.FirstAsync(l => l.Id == lineId, ct);
        var declaration = await _db.CustomsDeclarations.FirstAsync(d => d.Id == line.CustomsDeclarationId && d.ShipmentId == shipmentId, ct);
        if (declaration.Status is not (DeclarationStatus.Draft or DeclarationStatus.Rejected))
            throw new InvalidOperationException("Only draft or rejected declarations can be edited.");
        line.CommodityCode = commodityCode.Replace(" ", "", StringComparison.Ordinal);
        line.Description = description;
        line.StatisticalValue = statisticalValue;
        await _db.SaveChangesAsync(ct);
    }

    public Task RequestDocumentAsync(int shipmentId, DocumentKind kind, string user, CancellationToken ct) =>
        _bus.PublishAsync(new DocumentRequested(IntegrationEvent.NewId(), _clock.UtcNow, shipmentId, kind, user), ct);

    public Task PrintLabelsAsync(int shipmentId, int? packageId, string printer, string user, CancellationToken ct) =>
        _bus.PublishAsync(new LabelPrintRequested(IntegrationEvent.NewId(), _clock.UtcNow, shipmentId, packageId, printer, user), ct);

    public async Task<string?> BookLegAsync(int shipmentId, int legId, string user, CancellationToken ct)
    {
        var leg = await _db.Legs.Include(l => l.Carrier).FirstOrDefaultAsync(l => l.Id == legId && l.ShipmentId == shipmentId, ct);
        if (leg is null) return "Leg not found.";
        if (leg.Carrier is null) return "Select a carrier for the leg first.";
        if (leg.Status != LegStatus.Planned) return $"Leg is already {leg.Status}.";
        if (leg.Carrier.Integration == CarrierIntegration.Manual)
            return $"{leg.Carrier.Name} has no electronic booking; book by phone or e-mail and enter the reference.";
        await _bus.PublishAsync(new CarrierBookingRequested(IntegrationEvent.NewId(), _clock.UtcNow, shipmentId, legId, leg.Carrier.Id, user), ct);
        return null;
    }

    public async Task SetLegCarrierAsync(int shipmentId, int legId, int? carrierId, string? reference, CancellationToken ct)
    {
        var leg = await _db.Legs.FirstAsync(l => l.Id == legId && l.ShipmentId == shipmentId, ct);
        leg.CarrierId = carrierId;
        if (!string.IsNullOrWhiteSpace(reference))
        {
            leg.CarrierReference = reference.Trim();
            if (leg.Status == LegStatus.Planned) leg.Status = LegStatus.Confirmed;
        }
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Manual status from operations; applied by the event processor like any carrier status.</summary>
    public async Task ReportMilestoneAsync(int shipmentId, string code, DateTimeOffset occurredAt, string? locationCode, string? remarks, string user, CancellationToken ct)
    {
        if (!MilestoneCodes.IsKnown(code)) throw new ArgumentException($"Unknown milestone code {code}.", nameof(code));
        var number = await _db.Shipments.Where(s => s.Id == shipmentId).Select(s => s.Number).FirstAsync(ct);
        await _bus.PublishAsync(new StatusReported(IntegrationEvent.NewId(), occurredAt, number, null, null, code, locationCode, null, "user:" + user, remarks), ct);
    }

    public async Task<string?> CancelAsync(int shipmentId, string reason, string user, CancellationToken ct)
    {
        var s = await _db.Shipments.Include(x => x.Milestones).Include(x => x.Legs).FirstAsync(x => x.Id == shipmentId, ct);
        if (!ShipmentStateMachine.CanTransition(s.Status, ShipmentStatus.Cancelled))
            return $"A shipment that is {s.Status} cannot be cancelled.";
        var now = _clock.UtcNow;
        s.CancelReason = reason;
        ShipmentStateMachine.Apply(s, new Milestone { Code = MilestoneCodes.Cancelled, OccurredAt = now, RecordedAt = now, Source = user, Remarks = reason });
        foreach (var leg in s.Legs) leg.Status = LegStatus.Cancelled;
        await _db.SaveChangesAsync(ct);
        await _bus.PublishAsync(new ShipmentChanged(IntegrationEvent.NewId(), now, s.Id, s.Number, "cancelled", user), ct);
        return null;
    }
}

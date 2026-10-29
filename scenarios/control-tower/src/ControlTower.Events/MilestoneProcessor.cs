using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Integration.Messaging;

namespace Proseware.ControlTower.Events;

/// <summary>Applies reported statuses to shipments and their legs.</summary>
public sealed class MilestoneProcessor
{
    private static readonly HashSet<string> DepartureCodes = new() { MilestoneCodes.PickedUp, MilestoneCodes.Departed, MilestoneCodes.DepartedTerminal, MilestoneCodes.Loaded };
    private static readonly HashSet<string> ArrivalCodes = new() { MilestoneCodes.Arrived, MilestoneCodes.ArrivedAtTerminal, MilestoneCodes.Discharged, MilestoneCodes.Delivered };

    private readonly ControlTowerDbContext _db;
    private readonly ShipmentQueries _queries;
    private readonly IEventBus _bus;
    private readonly IClock _clock;
    private readonly ILogger<MilestoneProcessor> _logger;

    public MilestoneProcessor(ControlTowerDbContext db, ShipmentQueries queries, IEventBus bus, IClock clock, ILogger<MilestoneProcessor> logger)
    {
        _db = db;
        _queries = queries;
        _bus = bus;
        _clock = clock;
        _logger = logger;
    }

    public async Task<MilestoneResult?> ProcessAsync(StatusReported status, CancellationToken ct)
    {
        var shipment = await _queries.FindForStatusAsync(status.ShipmentNumber, status.CarrierReference, status.Sscc, ct);
        if (shipment is null)
        {
            _logger.LogWarning("Status {Code} from {Source} matches no shipment (number {Number}, carrier ref {Ref}, SSCC {Sscc})",
                status.MilestoneCode, status.Source, status.ShipmentNumber, status.CarrierReference, status.Sscc);
            return null;
        }

        var location = status.LocationCode is null
            ? null
            : await _db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Code == status.LocationCode, ct);

        var milestone = new Milestone
        {
            Code = MilestoneCodes.IsKnown(status.MilestoneCode) ? status.MilestoneCode : MilestoneCodes.EtaChanged,
            OccurredAt = status.OccurredAt,
            RecordedAt = _clock.UtcNow,
            LocationId = location?.Id,
            LocationText = Truncate(location?.City ?? status.LocationText, 100),
            Source = Truncate(status.Source, 60)!,
            Remarks = Truncate(status.Remarks, 350),
            VisibleToCustomer = status.MilestoneCode is not (MilestoneCodes.CustomsHeld or MilestoneCodes.Damaged)
        };

        var result = ShipmentStateMachine.Apply(shipment, milestone);
        if (result == MilestoneResult.Duplicate) return result;
        if (result == MilestoneResult.RejectedTransition)
            _logger.LogWarning("{Number}: {Code} from {Source} does not fit status {Status}; stored without status change", shipment.Number, milestone.Code, milestone.Source, shipment.Status);

        UpdateLegs(shipment, milestone);
        shipment.UpdatedAt = _clock.UtcNow;
        if (milestone.Code == MilestoneCodes.Delivered && !string.IsNullOrEmpty(status.Remarks) && status.Remarks.Contains("Signed by ", StringComparison.Ordinal))
            shipment.PodSignedBy = Truncate(status.Remarks[(status.Remarks.IndexOf("Signed by ", StringComparison.Ordinal) + 10)..], 70);
        if (shipment.Status == ShipmentStatus.Delivered) shipment.Eta = null;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // another processor changed the shipment at the same time; the broker redelivers the message
            _logger.LogInformation("{Number} was changed concurrently, status {Code} will be retried", shipment.Number, milestone.Code);
            throw;
        }

        await _bus.PublishAsync(new MilestoneRecorded(IntegrationEvent.NewId(), milestone.OccurredAt, shipment.Id, shipment.Number, shipment.CustomerId,
            milestone.Code, MilestoneCodes.Describe(milestone.Code), milestone.LocationText, shipment.Status), ct);
        return result;
    }

    internal static void UpdateLegs(Shipment shipment, Milestone milestone)
    {
        var legs = shipment.Legs.Where(l => l.Status != LegStatus.Cancelled).OrderBy(l => l.Sequence).ToList();
        if (legs.Count == 0) return;

        if (DepartureCodes.Contains(milestone.Code))
        {
            var leg = (milestone.LocationId is { } from ? legs.FirstOrDefault(l => l.FromId == from && l.Status is LegStatus.Planned or LegStatus.Confirmed) : null)
                      ?? legs.FirstOrDefault(l => l.Status is LegStatus.Planned or LegStatus.Confirmed);
            if (leg is null) return;
            // earlier legs must have been completed
            foreach (var before in legs.Where(l => l.Sequence < leg.Sequence && l.Status != LegStatus.Arrived))
            {
                before.Status = LegStatus.Arrived;
                before.ActualArrival ??= milestone.OccurredAt;
            }
            leg.Status = LegStatus.Departed;
            leg.ActualDeparture ??= milestone.OccurredAt;
        }
        else if (ArrivalCodes.Contains(milestone.Code))
        {
            var leg = (milestone.LocationId is { } to ? legs.FirstOrDefault(l => l.ToId == to && l.Status != LegStatus.Arrived) : null)
                      ?? legs.FirstOrDefault(l => l.Status == LegStatus.Departed)
                      ?? (milestone.Code == MilestoneCodes.Delivered ? legs[^1] : null);
            if (leg is null) return;
            leg.Status = LegStatus.Arrived;
            leg.ActualArrival ??= milestone.OccurredAt;
            leg.ActualDeparture ??= leg.PlannedDeparture;
            leg.Eta = null;
        }
    }

    private static string? Truncate(string? s, int max) => s is null || s.Length <= max ? s : s[..max];
}

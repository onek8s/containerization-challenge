using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Integration.Messaging;
using Proseware.ControlTower.Routing;

namespace Proseware.ControlTower.Events;

/// <summary>Recalculates the ETA of shipments carried by a vehicle when it reports a position.</summary>
public sealed class EtaService
{
    private static readonly ConcurrentDictionary<int, DateTimeOffset> LastCalculated = new();

    private readonly ControlTowerDbContext _db;
    private readonly ShipmentQueries _queries;
    private readonly EtaCalculator _calculator;
    private readonly IEventBus _bus;
    private readonly EventsOptions _options;
    private readonly ILogger<EtaService> _logger;

    public EtaService(ControlTowerDbContext db, ShipmentQueries queries, EtaCalculator calculator, IEventBus bus, IOptions<EventsOptions> options, ILogger<EtaService> logger)
    {
        _db = db;
        _queries = queries;
        _calculator = calculator;
        _bus = bus;
        _options = options.Value;
        _logger = logger;
    }

    /// <returns>Ids of the shipments on the vehicle.</returns>
    public async Task<IReadOnlyList<int>> UpdateAsync(VehiclePositionReported position, CancellationToken ct)
    {
        var shipmentIds = await _db.Legs.AsNoTracking()
            .Where(l => l.VehicleId == position.VehicleId && l.Status == LegStatus.Departed)
            .Select(l => l.ShipmentId)
            .Distinct()
            .ToListAsync(ct);
        if (shipmentIds.Count == 0) return shipmentIds;

        var due = shipmentIds
            .Where(id => !LastCalculated.TryGetValue(id, out var last) || position.OccurredAt - last >= TimeSpan.FromSeconds(_options.EtaRecalculateSeconds))
            .ToList();
        if (due.Count == 0) return shipmentIds;

        var shipments = await _queries.Load(tracking: true).Where(s => due.Contains(s.Id)).ToListAsync(ct);
        var updates = new List<EtaUpdated>();
        foreach (var shipment in shipments.Where(s => s.IsOpen))
        {
            LastCalculated[shipment.Id] = position.OccurredAt;
            var result = _calculator.Calculate(shipment, new GeoCoordinate(position.Latitude, position.Longitude), position.OccurredAt);
            if (result is null) continue;

            foreach (var legEta in result.Legs)
            {
                var leg = shipment.Legs.First(l => l.Id == legEta.LegId);
                leg.Eta = legEta.Arrival;
            }
            var previous = shipment.Eta;
            var moved = previous is null || (result.Eta - previous.Value).Duration() >= TimeSpan.FromMinutes(_options.EtaChangeThresholdMinutes);
            shipment.EtaCalculatedAt = position.OccurredAt;
            if (moved)
            {
                shipment.Eta = result.Eta;
                updates.Add(new EtaUpdated(IntegrationEvent.NewId(), position.OccurredAt, shipment.Id, shipment.Number, shipment.CustomerId, previous, result.Eta, result.RemainingKm));
            }
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogDebug("ETA update skipped for vehicle {Plate}: shipment changed concurrently", position.Plate);
            return shipmentIds;
        }
        foreach (var u in updates)
        {
            _logger.LogInformation("{Number}: ETA {Previous:yyyy-MM-dd HH:mm} -> {Eta:yyyy-MM-dd HH:mm} UTC, {Km} km to go", u.Number, u.PreviousEta, u.Eta, u.RemainingKm);
            await _bus.PublishAsync(u, ct);
        }
        return shipmentIds;
    }
}

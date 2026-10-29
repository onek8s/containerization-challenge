using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanArsdel.BuildingBlocks.Caching;
using VanArsdel.BuildingBlocks.Exceptions;
using VanArsdel.BuildingBlocks.Outbox;
using VanArsdel.Contracts.Inventory;
using VanArsdel.Contracts.Stores;
using VanArsdel.Inventory.Api.Data;
using VanArsdel.Inventory.Api.Domain;

namespace VanArsdel.Inventory.Api.Services
{
    public record StockDelta(string Sku, int Quantity);

    public record ReserveResult(bool Success, Guid? ReservationId, IReadOnlyList<StockDelta> ShortLines, string Message);

    public class StockService
    {
        private static readonly TimeSpan LockExpiry = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(10);

        private readonly InventoryDbContext _db;
        private readonly IDistributedLockProvider _locks;
        private readonly IOutbox _outbox;
        private readonly ILogger<StockService> _logger;

        public StockService(InventoryDbContext db, IDistributedLockProvider locks, IOutbox outbox, ILogger<StockService> logger)
        {
            _db = db;
            _locks = locks;
            _outbox = outbox;
            _logger = logger;
        }

        /// <summary>
        /// Applies on-hand changes at one location. Returns false when <paramref name="idempotencyKey"/>
        /// was processed before (POS files and ERP receipts are sometimes delivered twice).
        /// </summary>
        public async Task<bool> ApplyAsync(string locationCode, IEnumerable<StockDelta> deltas, string movementType, string reference,
            string user, string idempotencyKey, CancellationToken ct)
        {
            await using var handle = await AcquireAsync(locationCode, ct);

            if (idempotencyKey != null)
            {
                if (await _db.ProcessedMessages.AnyAsync(p => p.Key == idempotencyKey, ct)) return false;
                _db.ProcessedMessages.Add(new ProcessedMessage { Key = idempotencyKey, ProcessedUtc = DateTime.UtcNow });
            }

            var location = await _db.Locations.FindAsync(new object[] { locationCode }, ct)
                           ?? throw new DomainException($"Unknown location {locationCode}.");
            var grouped = deltas.GroupBy(d => d.Sku.ToUpperInvariant()).Select(g => new StockDelta(g.Key, g.Sum(x => x.Quantity))).ToList();
            var levels = await LoadLevelsAsync(locationCode, grouped.Select(d => d.Sku), ct);
            var now = DateTime.UtcNow;

            foreach (var d in grouped.Where(d => d.Quantity != 0))
            {
                var level = GetOrCreate(levels, locationCode, d.Sku, now);
                level.OnHand += d.Quantity;
                level.UpdatedUtc = now;
                if (level.OnHand < 0)
                {
                    // Sales of stock that the system did not know about (unreceived transfer, miscount); the count job fixes it.
                    _logger.LogWarning("Negative stock {Sku} at {Location}: {OnHand}", d.Sku, locationCode, level.OnHand);
                }
                _db.Movements.Add(new StockMovement
                {
                    LocationCode = locationCode,
                    Sku = d.Sku,
                    Quantity = d.Quantity,
                    Type = movementType,
                    Reference = Truncate(reference, 100),
                    CreatedBy = user,
                    CreatedUtc = now,
                });

                if (d.Quantity < 0 && level.MinLevel > 0 && level.Available < level.MinLevel && level.Available - d.Quantity >= level.MinLevel)
                {
                    _outbox.Add(new StockLevelLow { LocationCode = locationCode, Sku = d.Sku, Available = level.Available, MinLevel = level.MinLevel });
                }
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogDebug("{Type} {Reference} at {Location} ({Type2}): {Lines} lines", movementType, reference, location.Code, location.Type, grouped.Count);
            return true;
        }

        /// <summary>Sets counted quantities (stock count, initial load). Min/max are kept when not given.</summary>
        public async Task<int> SetCountsAsync(string locationCode, IEnumerable<StockCountLine> lines, string reference, string user, CancellationToken ct)
        {
            await using var handle = await AcquireAsync(locationCode, ct);
            var list = lines.ToList();
            var levels = await LoadLevelsAsync(locationCode, list.Select(l => l.Sku.ToUpperInvariant()), ct);
            var now = DateTime.UtcNow;
            foreach (var line in list)
            {
                var level = GetOrCreate(levels, locationCode, line.Sku.ToUpperInvariant(), now);
                var diff = line.Counted - level.OnHand;
                level.OnHand = line.Counted;
                if (line.MinLevel.HasValue) level.MinLevel = line.MinLevel.Value;
                if (line.MaxLevel.HasValue) level.MaxLevel = line.MaxLevel.Value;
                level.UpdatedUtc = now;
                if (diff != 0)
                {
                    _db.Movements.Add(new StockMovement { LocationCode = locationCode, Sku = level.Sku, Quantity = diff, Type = MovementTypes.StockCount, Reference = reference, CreatedBy = user, CreatedUtc = now });
                }
            }
            await _db.SaveChangesAsync(ct);
            return list.Count;
        }

        public async Task<ReserveResult> ReserveAsync(Guid orderId, Guid shipmentId, string locationCode, IReadOnlyList<StockDelta> lines,
            DateTime expiresUtc, CancellationToken ct)
        {
            await using var handle = await AcquireAsync(locationCode, ct);

            var existing = await _db.Reservations.Where(r => r.OrderId == orderId && r.ShipmentId == shipmentId && r.Status == ReservationStatus.Active).ToListAsync(ct);
            if (existing.Count > 0)
            {
                return new ReserveResult(true, existing[0].Id, Array.Empty<StockDelta>(), "Already reserved");
            }

            var location = await _db.Locations.FindAsync(new object[] { locationCode }, ct) ?? throw new DomainException($"Unknown location {locationCode}.");
            var safety = location.Type == LocationTypes.DistributionCentre ? 0 : location.SafetyStock;
            var levels = await LoadLevelsAsync(locationCode, lines.Select(l => l.Sku), ct);

            var shorts = lines
                .Select(l => new { l, available = levels.TryGetValue(l.Sku, out var s) ? s.Available - safety : 0 })
                .Where(x => x.available < x.l.Quantity)
                .Select(x => new StockDelta(x.l.Sku, x.l.Quantity - Math.Max(0, x.available)))
                .ToList();
            if (shorts.Count > 0)
            {
                return new ReserveResult(false, null, shorts, $"Not enough stock at {locationCode}");
            }

            var now = DateTime.UtcNow;
            var reservationId = Guid.NewGuid();
            foreach (var l in lines)
            {
                var level = levels[l.Sku];
                level.Reserved += l.Quantity;
                level.UpdatedUtc = now;
                _db.Reservations.Add(new Reservation
                {
                    Id = l == lines[0] ? reservationId : Guid.NewGuid(),
                    OrderId = orderId,
                    ShipmentId = shipmentId,
                    LocationCode = locationCode,
                    Sku = l.Sku,
                    Quantity = l.Quantity,
                    Status = ReservationStatus.Active,
                    CreatedUtc = now,
                    ExpiresUtc = expiresUtc,
                });
            }
            await _db.SaveChangesAsync(ct);
            return new ReserveResult(true, reservationId, Array.Empty<StockDelta>(), null);
        }

        public async Task<int> ReleaseAsync(Guid orderId, Guid? shipmentId, string reason, CancellationToken ct)
        {
            var reservations = await _db.Reservations
                .Where(r => r.OrderId == orderId && r.Status == ReservationStatus.Active && (shipmentId == null || r.ShipmentId == shipmentId))
                .ToListAsync(ct);
            foreach (var group in reservations.GroupBy(r => r.LocationCode))
            {
                await using var handle = await AcquireAsync(group.Key, ct);
                var levels = await LoadLevelsAsync(group.Key, group.Select(r => r.Sku), ct);
                foreach (var r in group)
                {
                    if (levels.TryGetValue(r.Sku, out var level)) level.Reserved = Math.Max(0, level.Reserved - r.Quantity);
                    r.Status = ReservationStatus.Released;
                    r.ClosedUtc = DateTime.UtcNow;
                }
                await _db.SaveChangesAsync(ct);
            }
            if (reservations.Count > 0) _logger.LogInformation("Released {Count} reservations of order {OrderId}: {Reason}", reservations.Count, orderId, reason);
            return reservations.Count;
        }

        /// <summary>The shipment left the location: the reserved units leave the on-hand stock.</summary>
        public async Task<int> CommitAsync(Guid orderId, Guid shipmentId, CancellationToken ct)
        {
            var reservations = await _db.Reservations
                .Where(r => r.OrderId == orderId && r.ShipmentId == shipmentId && r.Status == ReservationStatus.Active)
                .ToListAsync(ct);
            foreach (var group in reservations.GroupBy(r => r.LocationCode))
            {
                await using var handle = await AcquireAsync(group.Key, ct);
                var levels = await LoadLevelsAsync(group.Key, group.Select(r => r.Sku), ct);
                var now = DateTime.UtcNow;
                foreach (var r in group)
                {
                    var level = levels[r.Sku];
                    level.Reserved = Math.Max(0, level.Reserved - r.Quantity);
                    level.OnHand -= r.Quantity;
                    level.UpdatedUtc = now;
                    r.Status = ReservationStatus.Committed;
                    r.ClosedUtc = now;
                    _db.Movements.Add(new StockMovement { LocationCode = r.LocationCode, Sku = r.Sku, Quantity = -r.Quantity, Type = MovementTypes.Pick, Reference = orderId.ToString("N"), CreatedBy = "allocation", CreatedUtc = now });
                }
                await _db.SaveChangesAsync(ct);
            }
            return reservations.Count;
        }

        public async Task<int> ExpireReservationsAsync(CancellationToken ct)
        {
            var expired = await _db.Reservations.Where(r => r.Status == ReservationStatus.Active && r.ExpiresUtc < DateTime.UtcNow)
                .Select(r => r.OrderId).Distinct().ToListAsync(ct);
            var total = 0;
            foreach (var orderId in expired)
            {
                total += await ReleaseAsync(orderId, null, "expired", ct);
            }
            return total;
        }

        private async Task<Dictionary<string, StockLevel>> LoadLevelsAsync(string locationCode, IEnumerable<string> skus, CancellationToken ct)
        {
            var list = skus.Select(s => s.ToUpperInvariant()).Distinct().ToList();
            var rows = await _db.StockLevels.Where(s => s.LocationCode == locationCode && list.Contains(s.Sku)).ToListAsync(ct);
            return rows.ToDictionary(r => r.Sku, StringComparer.OrdinalIgnoreCase);
        }

        private StockLevel GetOrCreate(Dictionary<string, StockLevel> levels, string locationCode, string sku, DateTime now)
        {
            if (levels.TryGetValue(sku, out var level)) return level;
            level = new StockLevel { LocationCode = locationCode, Sku = sku, UpdatedUtc = now };
            _db.StockLevels.Add(level);
            levels[sku] = level;
            return level;
        }

        private async Task<IAsyncDisposable> AcquireAsync(string locationCode, CancellationToken ct)
        {
            var handle = await _locks.TryAcquireAsync("stock:" + locationCode, LockExpiry, LockWait, ct);
            return handle ?? throw new ConflictException($"Stock at {locationCode} is locked by another update. Try again.");
        }

        private static string Truncate(string s, int max) => s == null || s.Length <= max ? s : s.Substring(0, max);
    }

    public class StockCountLine
    {
        public string Sku { get; set; }
        public int Counted { get; set; }
        public int? MinLevel { get; set; }
        public int? MaxLevel { get; set; }
    }
}

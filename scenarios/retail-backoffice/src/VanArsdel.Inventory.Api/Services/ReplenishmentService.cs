using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VanArsdel.BuildingBlocks.Outbox;
using VanArsdel.Contracts.Inventory;
using VanArsdel.Contracts.Stores;
using VanArsdel.Inventory.Api.Data;
using VanArsdel.Inventory.Api.Domain;

namespace VanArsdel.Inventory.Api.Services
{
    /// <summary>
    /// Nightly min/max replenishment: stores below their minimum get a transfer proposal from the DC
    /// up to their maximum, limited by what the DC has. The ERP connector turns proposals into transfer orders.
    /// </summary>
    public class ReplenishmentService
    {
        private readonly InventoryDbContext _db;
        private readonly IOutbox _outbox;
        private readonly ILogger<ReplenishmentService> _logger;
        private readonly string _dcCode;

        public ReplenishmentService(InventoryDbContext db, IOutbox outbox, IConfiguration configuration, ILogger<ReplenishmentService> logger)
        {
            _db = db;
            _outbox = outbox;
            _logger = logger;
            _dcCode = configuration["Inventory:DistributionCentre"] ?? "DC01";
        }

        public async Task<List<ReplenishmentProposal>> RunAsync(IReadOnlyCollection<string> storeCodes, CancellationToken ct)
        {
            var stores = await _db.Locations.AsNoTracking()
                .Where(l => l.Active && l.Type != LocationTypes.DistributionCentre)
                .Where(l => storeCodes.Count == 0 || storeCodes.Contains(l.Code))
                .Select(l => l.Code).ToListAsync(ct);

            var below = await _db.StockLevels.AsNoTracking()
                .Where(s => stores.Contains(s.LocationCode) && s.MaxLevel > 0 && s.OnHand + s.InTransit < s.MinLevel)
                .ToListAsync(ct);
            var skus = below.Select(b => b.Sku).Distinct().ToList();
            var discontinued = await _db.Items.AsNoTracking().Where(i => skus.Contains(i.Sku) && i.Discontinued).Select(i => i.Sku).ToListAsync(ct);
            var dc = await _db.StockLevels.Where(s => s.LocationCode == _dcCode && skus.Contains(s.Sku)).ToDictionaryAsync(s => s.Sku, ct);
            var dcLeft = dc.ToDictionary(kv => kv.Key, kv => kv.Value.Available);

            var proposals = new List<ReplenishmentProposal>();
            var now = DateTime.UtcNow;
            foreach (var store in below.Where(b => !discontinued.Contains(b.Sku)).GroupBy(b => b.LocationCode).OrderBy(g => g.Key))
            {
                var proposal = new ReplenishmentProposal { Id = Guid.NewGuid(), StoreCode = store.Key, FromLocation = _dcCode, Status = "Proposed", CreatedUtc = now };
                foreach (var level in store.OrderBy(l => l.Sku))
                {
                    var need = level.MaxLevel - (level.OnHand + level.InTransit);
                    var left = dcLeft.TryGetValue(level.Sku, out var a) ? a : 0;
                    var qty = Math.Min(need, left);
                    if (qty <= 0) continue;
                    dcLeft[level.Sku] = left - qty;
                    proposal.Lines.Add(new ReplenishmentLine { Sku = level.Sku, Quantity = qty, OnHand = level.OnHand, MaxLevel = level.MaxLevel });
                }
                if (proposal.Lines.Count == 0) continue;

                _db.ReplenishmentProposals.Add(proposal);
                foreach (var line in proposal.Lines)
                {
                    var target = await _db.StockLevels.SingleAsync(s => s.LocationCode == store.Key && s.Sku == line.Sku, ct);
                    target.InTransit += line.Quantity;
                    dc[line.Sku].Reserved += line.Quantity;
                }
                _outbox.Add(new ReplenishmentProposed
                {
                    ProposalId = proposal.Id,
                    StoreCode = proposal.StoreCode,
                    FromLocation = proposal.FromLocation,
                    CreatedUtc = now,
                    Lines = proposal.Lines.Select(l => new StockLine { Sku = l.Sku, Quantity = l.Quantity }).ToList(),
                });
                proposals.Add(proposal);
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Replenishment: {Count} proposals, {Lines} lines", proposals.Count, proposals.Sum(p => p.Lines.Count));
            return proposals;
        }
    }
}

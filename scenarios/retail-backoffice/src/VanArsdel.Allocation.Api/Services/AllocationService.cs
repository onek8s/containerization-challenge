using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VanArsdel.Allocation.Api.Data;
using VanArsdel.Allocation.Api.Domain;
using VanArsdel.Contracts.Orders;
using VanArsdel.Inventory.Grpc;

namespace VanArsdel.Allocation.Api.Services
{
    public class AllocationOutcome
    {
        public bool Success { get; set; }
        public string Reason { get; set; }
        public List<AllocatedShipment> Shipments { get; set; } = new();
        public List<StockLineRef> ShortLines { get; set; } = new();
    }

    public class AllocationService
    {
        private const int MaxAttempts = 3;
        private readonly InventoryService.InventoryServiceClient _inventory;
        private readonly AllocationDbContext _db;
        private readonly AllocationEngine _engine = new();
        private readonly ILogger<AllocationService> _logger;

        public AllocationService(InventoryService.InventoryServiceClient inventory, AllocationDbContext db, ILogger<AllocationService> logger)
        {
            _inventory = inventory;
            _db = db;
            _logger = logger;
        }

        public async Task<AllocationOutcome> AllocateAsync(AllocateOrder order, CancellationToken ct)
        {
            var country = string.IsNullOrEmpty(order.ShipToCountry) ? "FI" : order.ShipToCountry;
            var rules = await _db.Rules.AsNoTracking().SingleOrDefaultAsync(r => r.Country == country, ct) ?? new AllocationRuleSet { Country = country };
            var engineRules = new AllocationRules
            {
                MaxShipments = rules.MaxShipments,
                AllowShipFromStore = rules.AllowShipFromStore,
                AllowCrossBorderStores = rules.AllowCrossBorderStores,
                StoreMinimumLeft = rules.StoreMinimumLeft,
            };
            var lines = order.Lines.Select(l => new StockLineRef { Sku = l.Sku.ToUpperInvariant(), Quantity = l.Quantity }).ToList();

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var sw = Stopwatch.StartNew();
                var stock = await LoadStockAsync(lines.Select(l => l.Sku), country, ct);
                var plan = _engine.Plan(lines, order.DeliveryMethod, country, order.CollectStoreCode, stock, engineRules);

                if (!plan.Success)
                {
                    await RecordAsync(order, attempt, plan, null, sw.ElapsedMilliseconds, ct);
                    return new AllocationOutcome { Success = false, Reason = plan.Reason, ShortLines = plan.ShortLines };
                }

                var shipments = plan.Shipments.Select(s => new AllocatedShipment
                {
                    ShipmentId = Guid.NewGuid(),
                    FromLocation = s.LocationCode,
                    FromLocationType = s.LocationType,
                    Lines = s.Lines,
                }).ToList();

                var reserved = new List<AllocatedShipment>();
                var raced = false;
                foreach (var shipment in shipments)
                {
                    var request = new ReserveRequest
                    {
                        OrderId = order.OrderId.ToString(),
                        ShipmentId = shipment.ShipmentId.ToString(),
                        LocationCode = shipment.FromLocation,
                        Expires = Timestamp.FromDateTime(DateTime.UtcNow.AddHours(rules.ReservationHours)),
                    };
                    request.Lines.AddRange(shipment.Lines.Select(l => new ReserveLine { Sku = l.Sku, Quantity = l.Quantity }));
                    var reply = await _inventory.ReserveAsync(request, cancellationToken: ct);
                    if (!reply.Success)
                    {
                        _logger.LogInformation("Reservation at {Location} for {OrderNo} failed ({Message}), attempt {Attempt}", shipment.FromLocation, order.OrderNo, reply.Message, attempt);
                        raced = true;
                        break;
                    }
                    reserved.Add(shipment);
                }

                if (!raced)
                {
                    await RecordAsync(order, attempt, plan, shipments, sw.ElapsedMilliseconds, ct);
                    return new AllocationOutcome { Success = true, Shipments = shipments };
                }

                // Someone else took the stock between the availability read and the reservation: undo and retry.
                await _inventory.ReleaseAsync(new ReleaseRequest { OrderId = order.OrderId.ToString(), Reason = "allocation retry" }, cancellationToken: ct);
                await RecordAsync(order, attempt, new AllocationPlan { Success = false, Strategy = plan.Strategy, Reason = "Reservation conflict" }, null, sw.ElapsedMilliseconds, ct);
            }

            return new AllocationOutcome { Success = false, Reason = "Stock changed during allocation", ShortLines = lines };
        }

        public Task CommitAsync(Guid orderId, Guid shipmentId, CancellationToken ct) =>
            _inventory.CommitAsync(new CommitRequest { OrderId = orderId.ToString(), ShipmentId = shipmentId.ToString() }, cancellationToken: ct).ResponseAsync;

        public Task ReleaseAsync(Guid orderId, string reason, CancellationToken ct) =>
            _inventory.ReleaseAsync(new ReleaseRequest { OrderId = orderId.ToString(), Reason = reason ?? string.Empty }, cancellationToken: ct).ResponseAsync;

        private async Task<List<StockAtLocation>> LoadStockAsync(IEnumerable<string> skus, string country, CancellationToken ct)
        {
            var request = new AvailabilityRequest { Country = country };
            request.Skus.AddRange(skus.Distinct());
            var reply = await _inventory.GetAvailabilityAsync(request, cancellationToken: ct);
            return reply.Locations.Select(l => new StockAtLocation
            {
                LocationCode = l.LocationCode,
                LocationType = l.LocationType,
                Country = l.Country,
                ShipFromStore = l.ShipFromStore,
                ClickAndCollect = l.ClickAndCollect,
                Available = l.Items.ToDictionary(i => i.Sku, i => Math.Max(0, i.Available - i.SafetyStock), StringComparer.OrdinalIgnoreCase),
            }).ToList();
        }

        private async Task RecordAsync(AllocateOrder order, int attempt, AllocationPlan plan, List<AllocatedShipment> shipments, long elapsed, CancellationToken ct)
        {
            _db.Decisions.Add(new AllocationDecision
            {
                OrderId = order.OrderId,
                OrderNo = order.OrderNo,
                Attempt = attempt,
                Success = shipments != null,
                Strategy = plan.Strategy,
                Reason = plan.Reason,
                DeliveryMethod = order.DeliveryMethod,
                Country = order.ShipToCountry,
                ShipmentsJson = shipments == null ? JsonConvert.SerializeObject(plan.ShortLines) : JsonConvert.SerializeObject(shipments),
                ElapsedMs = elapsed,
                CreatedUtc = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync(ct);
        }
    }
}

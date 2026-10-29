using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using VanArsdel.Inventory.Api.Services;

namespace VanArsdel.Inventory.Grpc
{
    [Authorize(Policy = VanArsdel.Inventory.Api.Policies.Allocate)]
    public class InventoryGrpcService : InventoryService.InventoryServiceBase
    {
        private readonly AvailabilityQueries _availability;
        private readonly StockService _stock;

        public InventoryGrpcService(AvailabilityQueries availability, StockService stock)
        {
            _availability = availability;
            _stock = stock;
        }

        public override async Task<AvailabilityReply> GetAvailability(AvailabilityRequest request, ServerCallContext context)
        {
            var result = await _availability.GetAsync(request.Skus.ToList(), request.LocationCodes.ToList(), request.Country,
                request.ShipFromStoreOnly, context.CancellationToken);
            var reply = new AvailabilityReply();
            foreach (var l in result)
            {
                var ls = new LocationStock
                {
                    LocationCode = l.Location.Code,
                    LocationType = l.Location.Type,
                    Country = l.Location.Country,
                    ShipFromStore = l.Location.ShipFromStore,
                    ClickAndCollect = l.Location.ClickAndCollect,
                };
                var safety = l.Location.Type == "DC" ? 0 : l.Location.SafetyStock;
                ls.Items.AddRange(l.Levels.Select(s => new SkuStock
                {
                    Sku = s.Sku,
                    OnHand = s.OnHand,
                    Reserved = s.Reserved,
                    Available = s.Available,
                    SafetyStock = safety,
                }));
                reply.Locations.Add(ls);
            }
            return reply;
        }

        public override async Task<ReserveReply> Reserve(ReserveRequest request, ServerCallContext context)
        {
            var expires = request.Expires?.ToDateTime() ?? DateTime.UtcNow.AddDays(3);
            var result = await _stock.ReserveAsync(Guid.Parse(request.OrderId), Guid.Parse(request.ShipmentId), request.LocationCode,
                request.Lines.Select(l => new StockDelta(l.Sku.ToUpperInvariant(), l.Quantity)).ToList(), expires, context.CancellationToken);

            var reply = new ReserveReply { Success = result.Success, ReservationId = result.ReservationId?.ToString() ?? string.Empty, Message = result.Message ?? string.Empty };
            reply.ShortLines.AddRange(result.ShortLines.Select(s => new ReserveLine { Sku = s.Sku, Quantity = s.Quantity }));
            return reply;
        }

        public override async Task<ReleaseReply> Release(ReleaseRequest request, ServerCallContext context)
        {
            Guid? shipment = string.IsNullOrEmpty(request.ShipmentId) ? null : Guid.Parse(request.ShipmentId);
            var count = await _stock.ReleaseAsync(Guid.Parse(request.OrderId), shipment, request.Reason, context.CancellationToken);
            return new ReleaseReply { Released = count };
        }

        public override async Task<CommitReply> Commit(CommitRequest request, ServerCallContext context)
        {
            var count = await _stock.CommitAsync(Guid.Parse(request.OrderId), Guid.Parse(request.ShipmentId), context.CancellationToken);
            return new CommitReply { Committed = count };
        }
    }
}

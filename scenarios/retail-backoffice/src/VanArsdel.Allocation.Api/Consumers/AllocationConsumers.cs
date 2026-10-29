using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using VanArsdel.Allocation.Api.Services;
using VanArsdel.Contracts.Orders;

namespace VanArsdel.Allocation.Api.Consumers
{
    public class AllocateOrderConsumer : IConsumer<AllocateOrder>
    {
        private readonly AllocationService _allocation;
        private readonly ILogger<AllocateOrderConsumer> _logger;

        public AllocateOrderConsumer(AllocationService allocation, ILogger<AllocateOrderConsumer> logger)
        {
            _allocation = allocation;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<AllocateOrder> context)
        {
            var outcome = await _allocation.AllocateAsync(context.Message, context.CancellationToken);
            if (outcome.Success)
            {
                _logger.LogInformation("Order {OrderNo} allocated to {Locations}", context.Message.OrderNo,
                    string.Join(", ", outcome.Shipments.ConvertAll(s => s.FromLocation)));
                await context.Publish(new OrderAllocated { OrderId = context.Message.OrderId, AllocationId = NewId.NextGuid(), Shipments = outcome.Shipments });
            }
            else
            {
                _logger.LogWarning("Order {OrderNo} could not be allocated: {Reason}", context.Message.OrderNo, outcome.Reason);
                await context.Publish(new OrderAllocationFailed { OrderId = context.Message.OrderId, Reason = outcome.Reason, ShortLines = outcome.ShortLines });
            }
        }
    }

    /// <summary>Reservations become stock movements when the shipment leaves; cancelled orders give the stock back.</summary>
    public class ReservationConsumers : IConsumer<ShipmentDispatched>, IConsumer<OrderCancelled>
    {
        private readonly AllocationService _allocation;

        public ReservationConsumers(AllocationService allocation)
        {
            _allocation = allocation;
        }

        public Task Consume(ConsumeContext<ShipmentDispatched> context) =>
            _allocation.CommitAsync(context.Message.OrderId, context.Message.ShipmentId, context.CancellationToken);

        public Task Consume(ConsumeContext<OrderCancelled> context) =>
            _allocation.ReleaseAsync(context.Message.OrderId, context.Message.Reason, context.CancellationToken);
    }
}

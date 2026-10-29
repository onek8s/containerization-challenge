using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Litware.OrderHub.Contracts;
using Litware.OrderHub.Core;
using Litware.OrderHub.Integration.Data;
using MassTransit;
using Serilog;

namespace Litware.OrderHub.Integration.Consumers
{
    /// <summary>Validates submitted orders and stores them (fulfilment service, queue orderhub.fulfilment.orders).</summary>
    public class OrderIntakeConsumer : IConsumer<OrderSubmitted>, IConsumer<ResubmitOrder>
    {
        private readonly IOrderStore _store;
        private readonly OrderValidator _validator;

        public OrderIntakeConsumer(IOrderStore store, OrderValidator validator)
        {
            _store = store;
            _validator = validator;
        }

        public async Task Consume(ConsumeContext<OrderSubmitted> context)
        {
            var message = context.Message;
            var existing = await _store.FindByExternalRefAsync(message.Channel, message.ExternalRef);
            if (existing != null)
            {
                Log.Information("{Channel} order {ExternalRef} was already received as {OrderId}, ignored", message.Channel, message.ExternalRef, existing.OrderId);
                await _store.AddEventAsync(existing.OrderId, "Duplicate", "Received again at " + message.SubmittedUtc.ToString("u"));
                return;
            }

            var problems = await ValidateAsync(message.Order);
            var order = new OrderRecord
            {
                OrderId = message.OrderId,
                Channel = message.Channel,
                ExternalRef = message.ExternalRef,
                ReceivedUtc = message.SubmittedUtc,
                Status = problems.Count == 0 ? OrderStatus.Accepted : OrderStatus.Rejected,
                StatusReason = problems.Count == 0 ? null : string.Join(" ", problems),
                Document = message.Order
            };

            if (!await _store.InsertAsync(order))
            {
                Log.Information("{Channel} order {ExternalRef} was stored by another consumer, ignored", message.Channel, message.ExternalRef);
                return;
            }

            await _store.AddEventAsync(order.OrderId, "Received", message.Channel + " order " + message.ExternalRef);
            await PublishResultAsync(context, order);
        }

        public async Task Consume(ConsumeContext<ResubmitOrder> context)
        {
            var order = await _store.FindAsync(context.Message.OrderId);
            if (order == null)
            {
                Log.Warning("Resubmit for unknown order {OrderId}", context.Message.OrderId);
                return;
            }
            if (order.Status != OrderStatus.Rejected)
            {
                Log.Information("Order {OrderId} is {Status}, resubmit ignored", order.OrderId, order.Status);
                return;
            }

            var problems = await ValidateAsync(order.Document);
            await _store.AddEventAsync(order.OrderId, "Resubmitted", "By " + context.Message.RequestedBy);
            order.Status = problems.Count == 0 ? OrderStatus.Accepted : OrderStatus.Rejected;
            order.StatusReason = problems.Count == 0 ? null : string.Join(" ", problems);
            await _store.UpdateValidationAsync(order.OrderId, order.Status, order.StatusReason);

            order = await _store.FindAsync(order.OrderId);
            await PublishResultAsync(context, order);
        }

        private async Task<IList<string>> ValidateAsync(OrderDocument document)
        {
            var products = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);
            foreach (var sku in document.Lines.Select(l => l.Sku).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct())
            {
                products[sku] = await _store.FindProductAsync(sku);
            }
            return _validator.Validate(document, sku => products.TryGetValue(sku, out var p) ? p : null);
        }

        private async Task PublishResultAsync(ConsumeContext context, OrderRecord order)
        {
            if (order.Status == OrderStatus.Accepted)
            {
                Log.Information("Order {OrderNumber} accepted ({Channel} {ExternalRef})", order.OrderNumber, order.Channel, order.ExternalRef);
                await _store.AddEventAsync(order.OrderId, "Accepted", order.OrderNumber);
                await context.Publish<OrderAccepted>(new { order.OrderId, order.OrderNumber, order.Channel });
            }
            else
            {
                Log.Warning("{Channel} order {ExternalRef} rejected: {Reason}", order.Channel, order.ExternalRef, order.StatusReason);
                await _store.AddEventAsync(order.OrderId, "Rejected", order.StatusReason);
                await context.Publish<OrderRejected>(new { order.OrderId, order.Channel, order.ExternalRef, Reason = order.StatusReason });
            }
        }
    }
}

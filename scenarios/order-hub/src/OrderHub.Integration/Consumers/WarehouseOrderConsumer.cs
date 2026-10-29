using System.Threading.Tasks;
using Litware.OrderHub.Contracts;
using Litware.OrderHub.Core;
using Litware.OrderHub.Core.Warehouse;
using Litware.OrderHub.Integration.Data;
using Litware.OrderHub.Integration.Sftp;
using MassTransit;
using Serilog;

namespace Litware.OrderHub.Integration.Consumers
{
    /// <summary>Sends accepted orders to the 3PL warehouse SFTP (fulfilment service, queue orderhub.fulfilment.warehouse).</summary>
    public class WarehouseOrderConsumer : IConsumer<OrderAccepted>, IConsumer<ResendToWarehouse>
    {
        private readonly IOrderStore _store;
        private readonly ISftpConnector _warehouse;
        private readonly string _inboxDirectory;
        private readonly string _clientCode;

        public WarehouseOrderConsumer(IOrderStore store, ISftpConnector warehouse, string inboxDirectory, string clientCode)
        {
            _store = store;
            _warehouse = warehouse;
            _inboxDirectory = inboxDirectory;
            _clientCode = clientCode;
        }

        public Task Consume(ConsumeContext<OrderAccepted> context)
        {
            return SendAsync(context.Message.OrderId, false, null);
        }

        public Task Consume(ConsumeContext<ResendToWarehouse> context)
        {
            return SendAsync(context.Message.OrderId, true, context.Message.RequestedBy);
        }

        private async Task SendAsync(System.Guid orderId, bool resend, string requestedBy)
        {
            var order = await _store.FindAsync(orderId);
            if (order == null || order.OrderNumber == null)
            {
                Log.Warning("Order {OrderId} not found or has no order number, not sent to the warehouse", orderId);
                return;
            }
            if (!resend && order.Status != OrderStatus.Accepted)
            {
                Log.Information("Order {OrderNumber} is already {Status}", order.OrderNumber, order.Status);
                return;
            }

            string fileName = WarehouseOrderFile.FileName(order);
            byte[] content = WarehouseOrderFile.ToBytes(WarehouseOrderFile.Build(order, _clientCode));

            Resilience.Sftp("Warehouse order " + order.OrderNumber).Execute(() =>
            {
                using (var session = _warehouse.Connect())
                {
                    session.Upload(_inboxDirectory, fileName, content);
                }
            });

            await _store.MarkSentToWarehouseAsync(order.OrderId, fileName);
            await _store.AddEventAsync(order.OrderId, resend ? "ResentToWarehouse" : "SentToWarehouse",
                resend ? fileName + " by " + requestedBy : fileName);
            Log.Information("Order {OrderNumber} sent to the warehouse as {File}", order.OrderNumber, fileName);
        }
    }
}

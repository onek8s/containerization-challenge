using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Litware.OrderHub.Contracts;
using Litware.OrderHub.Core.Warehouse;
using Litware.OrderHub.Integration.Data;
using Litware.OrderHub.Integration.Sftp;
using MassTransit;
using Serilog;

namespace Litware.OrderHub.Integration.Consumers
{
    /// <summary>Picks up shipment confirmations from the warehouse SFTP "out" folder (fulfilment service).</summary>
    public class ShipmentConfirmationPoller
    {
        private readonly IOrderStore _store;
        private readonly ISftpConnector _warehouse;
        private readonly IPublishEndpoint _bus;
        private readonly string _outboxDirectory;

        public ShipmentConfirmationPoller(IOrderStore store, ISftpConnector warehouse, IPublishEndpoint bus, string outboxDirectory)
        {
            _store = store;
            _warehouse = warehouse;
            _bus = bus;
            _outboxDirectory = outboxDirectory.TrimEnd('/');
        }

        /// <returns>Number of files processed.</returns>
        public async Task<int> PollAsync()
        {
            int processed = 0;
            using (var session = Resilience.Sftp("Warehouse confirmations").Execute(() => _warehouse.Connect()))
            {
                foreach (var path in session.ListFiles(_outboxDirectory, ".csv"))
                {
                    byte[] content = session.Download(path);
                    try
                    {
                        await ProcessAsync(path, content);
                        session.Move(path, _outboxDirectory + "/archive");
                    }
                    catch (FormatException ex)
                    {
                        Log.Error(ex, "Shipment confirmation {File} cannot be read, moved to error", path);
                        session.Move(path, _outboxDirectory + "/error");
                    }
                    processed++;
                }
            }
            return processed;
        }

        private async Task ProcessAsync(string path, byte[] content)
        {
            var confirmations = ShipmentConfirmationFile.Read(new StringReader(Encoding.UTF8.GetString(content)));
            foreach (var confirmation in confirmations)
            {
                var order = await _store.FindByNumberAsync(confirmation.OrderNumber);
                if (order == null)
                {
                    Log.Warning("{File}: unknown order {OrderNumber} skipped", path, confirmation.OrderNumber);
                    continue;
                }
                if (order.TrackingNumber == confirmation.TrackingNumber)
                {
                    Log.Information("{File}: shipment {Tracking} of {OrderNumber} already recorded", path, confirmation.TrackingNumber, order.OrderNumber);
                    continue;
                }

                await _store.MarkShippedAsync(order.OrderId, confirmation.Carrier, confirmation.TrackingNumber, confirmation.ShippedUtc, confirmation.Lines);
                await _store.AddEventAsync(order.OrderId, "Shipped", confirmation.Carrier + " " + confirmation.TrackingNumber);

                int ordered = order.Document.Lines.Sum(l => l.Quantity);
                int shipped = confirmation.Lines.Sum(l => l.Quantity);
                if (shipped < ordered)
                {
                    await _store.AddEventAsync(order.OrderId, "ShortShipped", shipped + " of " + ordered + " units");
                }

                await _bus.Publish<OrderShipped>(new
                {
                    order.OrderId,
                    order.OrderNumber,
                    order.Channel,
                    order.ExternalRef,
                    confirmation.Carrier,
                    confirmation.TrackingNumber,
                    confirmation.ShippedUtc,
                    Lines = confirmation.Lines
                });
                Log.Information("Order {OrderNumber} shipped with {Carrier} {Tracking}", order.OrderNumber, confirmation.Carrier, confirmation.TrackingNumber);
            }
        }
    }
}

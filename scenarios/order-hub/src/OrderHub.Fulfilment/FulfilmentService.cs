using System;
using System.Configuration;
using Litware.OrderHub.Core;
using Litware.OrderHub.Integration;
using Litware.OrderHub.Integration.Configuration;
using Litware.OrderHub.Integration.Consumers;
using Litware.OrderHub.Integration.Data;
using Litware.OrderHub.Integration.Messaging;
using Litware.OrderHub.Integration.Sftp;
using MassTransit;

namespace Litware.OrderHub.Fulfilment
{
    public class FulfilmentService
    {
        private readonly SettingsReader _settings;
        private IBusControl _bus;
        private PollingLoop _confirmationLoop;

        public FulfilmentService(SettingsReader settings)
        {
            _settings = settings;
        }

        public void Start()
        {
            var store = new SqlOrderStore(ConfigurationManager.ConnectionStrings["OrderHub"].ConnectionString);
            var validator = new OrderValidator(
                _settings.GetList("Orders.ShipToCountries", "FI,SE,NO,DK,EE,DE"),
                _settings.GetList("Orders.Currencies", "EUR,SEK,NOK,DKK"));
            var warehouse = new SshNetSftpConnector(SftpSettings.Read(_settings, "Warehouse.Sftp"));
            string inbox = _settings.Get("Warehouse.InDirectory", "/in");
            string clientCode = _settings.Get("Warehouse.ClientCode", "LITWARE");

            _bus = HubBus.Create(BusSettings.Read(_settings), (cfg, host) =>
            {
                cfg.ReceiveEndpoint(host, HubBus.Queues.FulfilmentOrders, e =>
                {
                    e.UseHubRetry();
                    e.Consumer(() => new OrderIntakeConsumer(store, validator));
                });
                cfg.ReceiveEndpoint(host, HubBus.Queues.FulfilmentWarehouse, e =>
                {
                    e.UseHubRetry();
                    // One SFTP upload at a time; the 3PL account allows a few concurrent sessions only.
                    e.PrefetchCount = 1;
                    e.Consumer(() => new WarehouseOrderConsumer(store, warehouse, inbox, clientCode));
                });
            });
            _bus.Start();

            var poller = new ShipmentConfirmationPoller(store, warehouse, _bus, _settings.Get("Warehouse.OutDirectory", "/out"));
            _confirmationLoop = new PollingLoop("Shipment confirmations",
                _settings.GetTime("Warehouse.PollInterval", TimeSpan.FromMinutes(5)),
                () => poller.PollAsync());
            _confirmationLoop.Start();
        }

        public void Stop()
        {
            _confirmationLoop?.Stop();
            _bus?.Stop(TimeSpan.FromSeconds(30));
        }
    }
}

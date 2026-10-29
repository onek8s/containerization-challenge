using System;
using System.Configuration;
using Litware.OrderHub.Integration;
using Litware.OrderHub.Integration.Configuration;
using Litware.OrderHub.Integration.Consumers;
using Litware.OrderHub.Integration.Data;
using Litware.OrderHub.Integration.Messaging;
using Litware.OrderHub.Integration.Partners;
using Litware.OrderHub.Integration.Reporting;
using MassTransit;

namespace Litware.OrderHub.Partners
{
    public class PartnersService
    {
        private readonly SettingsReader _settings;
        private IBusControl _bus;
        private IPartnerGateway _partner;
        private PollingLoop _reportLoop;

        public PartnersService(SettingsReader settings)
        {
            _settings = settings;
        }

        public void Start()
        {
            var store = new SqlOrderStore(ConfigurationManager.ConnectionStrings["OrderHub"].ConnectionString);
            var renderer = SsrsInvoiceRenderer.Create(_settings);
            _partner = PartnerGatewayFactory.Create(_settings);

            _bus = HubBus.Create(BusSettings.Read(_settings), (cfg, host) =>
            {
                cfg.ReceiveEndpoint(host, HubBus.Queues.PartnersInvoicing, e =>
                {
                    e.UseHubRetry();
                    e.PrefetchCount = 2;
                    e.Consumer(() => new InvoiceConsumer(store, renderer));
                });
                cfg.ReceiveEndpoint(host, HubBus.Queues.PartnersDispatch, e =>
                {
                    e.UseHubRetry();
                    e.Consumer(() => new PartnerDispatchConsumer(store, _partner));
                });
            });
            _bus.Start();

            var receiver = new PartnerOrderReceiver(_bus);
            _partner.StartReceiving(receiver.HandleAsync);

            var job = new DailySalesReportJob(store, _settings.GetTime("Reports.DailySalesAt", new TimeSpan(2, 0, 0)));
            _reportLoop = new PollingLoop("Daily sales workbook", TimeSpan.FromMinutes(1), () => job.RunIfDueAsync());
            _reportLoop.Start();
        }

        public void Stop()
        {
            _reportLoop?.Stop();
            _partner?.StopAsync().GetAwaiter().GetResult();
            _bus?.Stop(TimeSpan.FromSeconds(30));
            _partner?.Dispose();
        }
    }
}

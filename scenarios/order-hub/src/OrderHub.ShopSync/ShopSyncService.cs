using System;
using System.IO;
using System.Threading;
using Litware.OrderHub.Integration;
using Litware.OrderHub.Integration.Configuration;
using Litware.OrderHub.Integration.Consumers;
using Litware.OrderHub.Integration.Data;
using Litware.OrderHub.Integration.Mail;
using Litware.OrderHub.Integration.Messaging;
using Litware.OrderHub.Integration.Sftp;
using Litware.OrderHub.Integration.Webshop;
using MassTransit;
using System.Configuration;

namespace Litware.OrderHub.ShopSync
{
    public class ShopSyncService
    {
        private readonly SettingsReader _settings;
        private IBusControl _bus;
        private PollingLoop _stockLoop;
        private WebshopApiClient _webshop;

        public ShopSyncService(SettingsReader settings)
        {
            _settings = settings;
        }

        public void Start()
        {
            var store = new SqlOrderStore(ConfigurationManager.ConnectionStrings["OrderHub"].ConnectionString);
            _webshop = WebshopApiClient.Create(_settings);
            var mailer = new CustomerMailer(
                _settings.Get("Mail.TemplateFolder", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates")),
                MailSettings.Read(_settings));

            ISftpConnector marketplace = _settings.GetBool("Marketplace.Enabled", true)
                ? new SshNetSftpConnector(SftpSettings.Read(_settings, "Marketplace.Sftp"))
                : null;

            _bus = HubBus.Create(BusSettings.Read(_settings), (cfg, host) =>
            {
                cfg.ReceiveEndpoint(host, HubBus.Queues.ShopSyncShipments, e =>
                {
                    e.UseHubRetry();
                    e.Consumer(() => new ShopShipmentConsumer(store, _webshop, mailer, marketplace,
                        _settings.Get("Marketplace.ConfirmationDirectory", "/confirmations")));
                });
                cfg.ReceiveEndpoint(host, HubBus.Queues.ShopSyncRejections, e =>
                {
                    e.UseHubRetry();
                    e.Consumer(() => new ShopRejectionConsumer(_webshop));
                });
            });
            _bus.Start();

            if (_settings.GetBool("Stock.Enabled", true))
            {
                var job = new StockSyncJob(
                    new SshNetSftpConnector(SftpSettings.Read(_settings, "Warehouse.Sftp")),
                    _settings.Get("Warehouse.StockDirectory", "/out/stock"),
                    _webshop, marketplace,
                    _settings.Get("Marketplace.StockDirectory", "/stock"),
                    _settings.GetInt("Stock.SafetyStock", 2));
                _stockLoop = new PollingLoop("Stock sync", _settings.GetTime("Stock.PollInterval", TimeSpan.FromMinutes(10)),
                    () => job.RunAsync(CancellationToken.None));
                _stockLoop.Start();
            }
        }

        public void Stop()
        {
            _stockLoop?.Stop();
            _bus?.Stop(TimeSpan.FromSeconds(30));
            _webshop?.Dispose();
        }
    }
}

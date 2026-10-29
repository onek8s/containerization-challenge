using System;
using Litware.OrderHub.Integration;
using Litware.OrderHub.Integration.Configuration;
using Litware.OrderHub.Integration.Consumers;
using Litware.OrderHub.Integration.Messaging;
using Litware.OrderHub.Integration.Sftp;
using MassTransit;
using Microsoft.Owin.Hosting;
using Serilog;

namespace Litware.OrderHub.Intake
{
    public class IntakeService
    {
        private readonly SettingsReader _settings;
        private IBusControl _bus;
        private IDisposable _webApp;
        private PollingLoop _marketplaceLoop;

        public IntakeService(SettingsReader settings)
        {
            _settings = settings;
        }

        public void Start()
        {
            _bus = HubBus.Create(BusSettings.Read(_settings));
            _bus.Start();

            string listenUrl = _settings.Get("Intake.ListenUrl", "http://+:8085/");
            var api = new Api.ApiOptions
            {
                Bus = _bus,
                WebshopSecret = _settings.Require("Webshop.WebhookSecret")
            };
            _webApp = WebApp.Start(listenUrl, app => Api.Startup.Configure(app, api));
            Log.Information("Webhook endpoint listening on {Url}", listenUrl);

            if (_settings.GetBool("Marketplace.Enabled", true))
            {
                var poller = new MarketplaceOrderPoller(
                    new SshNetSftpConnector(SftpSettings.Read(_settings, "Marketplace.Sftp")),
                    _bus,
                    _settings.Get("Marketplace.ExportDirectory", "/orders"));
                _marketplaceLoop = new PollingLoop("Marketplace poll",
                    _settings.GetTime("Marketplace.PollInterval", TimeSpan.FromMinutes(2)),
                    () => poller.PollAsync());
                _marketplaceLoop.Start();
            }
        }

        public void Stop()
        {
            _marketplaceLoop?.Stop();
            _webApp?.Dispose();
            _bus?.Stop(TimeSpan.FromSeconds(30));
        }
    }
}

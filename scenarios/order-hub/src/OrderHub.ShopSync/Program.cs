using System;
using System.Configuration;
using Litware.OrderHub.Integration.Configuration;
using Serilog;
using Topshelf;

namespace Litware.OrderHub.ShopSync
{
    internal static class Program
    {
        private static int Main()
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.AppSettings()
                .Enrich.WithProperty("Service", "ShopSync")
                .CreateLogger();

            var settings = new SettingsReader(key => ConfigurationManager.AppSettings[key]);

            var exitCode = HostFactory.Run(host =>
            {
                host.UseSerilog();
                host.Service<ShopSyncService>(service =>
                {
                    service.ConstructUsing(() => new ShopSyncService(settings));
                    service.WhenStarted(s => s.Start());
                    service.WhenStopped(s => s.Stop());
                });

                host.SetServiceName("OrderHubShopSync");
                host.SetDisplayName("Order Hub Shop Sync");
                host.SetDescription("Reports shipments and stock levels back to the webshop and the marketplace and sends customer shipment e-mails.");
                host.StartAutomaticallyDelayed();
                host.EnableServiceRecovery(recovery =>
                {
                    recovery.RestartService(1);
                    recovery.RestartService(5);
                    recovery.SetResetPeriod(1);
                });
                host.OnException(ex => Log.Fatal(ex, "Unhandled exception"));
            });

            Log.CloseAndFlush();
            return (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode());
        }
    }
}

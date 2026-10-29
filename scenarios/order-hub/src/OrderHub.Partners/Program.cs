using System;
using System.Configuration;
using Litware.OrderHub.Integration.Configuration;
using Serilog;
using Topshelf;

namespace Litware.OrderHub.Partners
{
    internal static class Program
    {
        private static int Main()
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.AppSettings()
                .Enrich.WithProperty("Service", "Partners")
                .CreateLogger();

            var settings = new SettingsReader(key => ConfigurationManager.AppSettings[key]);

            var exitCode = HostFactory.Run(host =>
            {
                host.UseSerilog();
                host.Service<PartnersService>(service =>
                {
                    service.ConstructUsing(() => new PartnersService(settings));
                    service.WhenStarted(s => s.Start());
                    service.WhenStopped(s => s.Stop());
                });

                host.SetServiceName("OrderHubPartners");
                host.SetDisplayName("Order Hub Partners");
                host.SetDescription("Invoices shipped orders, exchanges messages with retail partners and creates the sales reports.");
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

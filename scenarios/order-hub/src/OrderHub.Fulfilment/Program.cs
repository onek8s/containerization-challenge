using System;
using System.Configuration;
using Litware.OrderHub.Integration.Configuration;
using Serilog;
using Topshelf;

namespace Litware.OrderHub.Fulfilment
{
    internal static class Program
    {
        private static int Main()
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.AppSettings()
                .Enrich.WithProperty("Service", "Fulfilment")
                .CreateLogger();

            var settings = new SettingsReader(key => ConfigurationManager.AppSettings[key]);

            var exitCode = HostFactory.Run(host =>
            {
                host.UseSerilog();
                host.Service<FulfilmentService>(service =>
                {
                    service.ConstructUsing(() => new FulfilmentService(settings));
                    service.WhenStarted(s => s.Start());
                    service.WhenStopped(s => s.Stop());
                });

                host.SetServiceName("OrderHubFulfilment");
                host.SetDisplayName("Order Hub Fulfilment");
                host.SetDescription("Validates orders, sends them to the warehouse and records shipment confirmations.");
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

using System;
using System.Configuration;
using Litware.OrderHub.Integration.Configuration;
using Serilog;
using Topshelf;

namespace Litware.OrderHub.Intake
{
    internal static class Program
    {
        private static int Main()
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.AppSettings()
                .Enrich.WithProperty("Service", "Intake")
                .CreateLogger();

            var settings = new SettingsReader(key => ConfigurationManager.AppSettings[key]);

            var exitCode = HostFactory.Run(host =>
            {
                host.UseSerilog();
                host.Service<IntakeService>(service =>
                {
                    service.ConstructUsing(() => new IntakeService(settings));
                    service.WhenStarted(s => s.Start());
                    service.WhenStopped(s => s.Stop());
                });

                host.SetServiceName("OrderHubIntake");
                host.SetDisplayName("Order Hub Intake");
                host.SetDescription("Receives webshop webhooks and marketplace order files and passes them to the order hub.");
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

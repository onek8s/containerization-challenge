using System;
using Tailspin.DealerOrders.Infrastructure.Logging;
using Topshelf;

namespace Tailspin.DealerOrders.AxConnector
{
    internal static class Program
    {
        private static int Main()
        {
            LogSetup.Configure("AxConnector");

            var exitCode = HostFactory.Run(host =>
            {
                host.UseLog4Net();
                host.Service<ConnectorService>(service =>
                {
                    service.ConstructUsing(() => new ConnectorService());
                    service.WhenStarted(s => s.Start());
                    service.WhenStopped(s => s.Stop());
                });

                // The service must run as the Business Connector proxy account; Install-Service.ps1 passes it.
                host.RunAsPrompt();
                host.SetServiceName("TailspinDealerAxConnector");
                host.SetDisplayName("Tailspin Dealer Orders AX Connector");
                host.SetDescription("Creates customers, quotations, sales orders and reservations in Dynamics AX and reads order status back.");
                host.DependsOnEventLog();
                host.StartAutomaticallyDelayed();
                host.EnableServiceRecovery(recovery =>
                {
                    recovery.RestartService(1);
                    recovery.RestartService(5);
                    recovery.RestartService(15);
                    recovery.SetResetPeriod(1);
                });
            });

            return (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode());
        }
    }
}

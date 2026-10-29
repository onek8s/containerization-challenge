using System;
using Tailspin.DealerOrders.Infrastructure.Logging;
using Topshelf;

namespace Tailspin.DealerOrders.Notifications
{
    internal static class Program
    {
        private static int Main()
        {
            LogSetup.Configure("Notifications");

            var exitCode = HostFactory.Run(host =>
            {
                host.UseLog4Net();
                host.Service<NotificationService>(service =>
                {
                    service.ConstructUsing(() => new NotificationService());
                    service.WhenStarted(s => s.Start());
                    service.WhenStopped(s => s.Stop());
                });

                host.RunAsNetworkService();
                host.SetServiceName("TailspinDealerNotifications");
                host.SetDisplayName("Tailspin Dealer Orders Notifications");
                host.SetDescription("Sends dealer and sales desk mail and pushes order status changes to the dealer portal.");
                host.StartAutomatically();
                host.EnableServiceRecovery(recovery =>
                {
                    recovery.RestartService(1);
                    recovery.RestartService(5);
                    recovery.SetResetPeriod(1);
                });
            });

            return (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode());
        }
    }
}

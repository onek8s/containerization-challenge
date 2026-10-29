using System;
using System.Linq;
using Tailspin.DealerOrders.Infrastructure.Logging;
using Topshelf;

namespace Tailspin.DealerOrders.PriceSync
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            LogSetup.Configure("PriceSync");

            // "run <job>" runs one job once and exits (used after price list changes and in test environments).
            if (args.Length == 2 && string.Equals(args[0], "run", StringComparison.OrdinalIgnoreCase))
                return SyncService.RunOnce(args[1]);

            var exitCode = HostFactory.Run(host =>
            {
                host.UseLog4Net();
                host.Service<SyncService>(service =>
                {
                    service.ConstructUsing(() => new SyncService());
                    service.WhenStarted(s => s.Start());
                    service.WhenStopped(s => s.Stop());
                });
                host.RunAsPrompt();
                host.SetServiceName("TailspinDealerPriceSync");
                host.SetDisplayName("Tailspin Dealer Orders Price Sync");
                host.SetDescription("Copies prices, stock, exchange rates and dealer balances from Dynamics AX to the dealer portal cache and runs the scheduled jobs.");
                host.StartAutomaticallyDelayed();
                host.EnableServiceRecovery(r => { r.RestartService(1); r.RestartService(5); r.SetResetPeriod(1); });
            });
            return (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode());
        }
    }
}

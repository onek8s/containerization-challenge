using System;
using Tailspin.DealerOrders.Infrastructure.Logging;
using Topshelf;

namespace Tailspin.DealerOrders.EdiGateway
{
    internal static class Program
    {
        private static int Main()
        {
            LogSetup.Configure("EdiGateway");

            var exitCode = HostFactory.Run(host =>
            {
                host.UseLog4Net();
                host.Service<GatewayService>(service =>
                {
                    service.ConstructUsing(() => new GatewayService());
                    service.WhenStarted(s => s.Start());
                    service.WhenStopped(s => s.Stop());
                });

                // The URL reservation for http://+:4080/as2/ is made by Install-Service.ps1 (netsh http add urlacl).
                host.RunAsNetworkService();
                host.SetServiceName("TailspinDealerEdiGateway");
                host.SetDisplayName("Tailspin Dealer Orders EDI Gateway");
                host.SetDescription("Receives X12 purchase orders from dealers over AS2 and SFTP and sends acknowledgments and ship notices.");
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

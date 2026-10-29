using System;
using System.Linq;
using System.Runtime.InteropServices;
using NLog.Web;
using Topshelf;

namespace Fabrikam.Housing.DocumentService
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            var logger = NLogBuilder.ConfigureNLog(System.IO.Path.Combine(AppContext.BaseDirectory, "nlog.config")).GetCurrentClassLogger();

            // --console runs the service in the foreground (developer machines and troubleshooting).
            if (args.Contains("--console", StringComparer.OrdinalIgnoreCase) || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var host = new DocumentServiceHost(args.Where(a => !a.Equals("--console", StringComparison.OrdinalIgnoreCase)).ToArray());
                try
                {
                    host.RunConsole();
                    return 0;
                }
                catch (Exception ex)
                {
                    logger.Fatal(ex, "Document service stopped because of an exception");
                    return 1;
                }
                finally
                {
                    NLog.LogManager.Shutdown();
                }
            }

            var exitCode = HostFactory.Run(x =>
            {
                x.UseNLog();
                x.Service<DocumentServiceHost>(s =>
                {
                    s.ConstructUsing(_ => new DocumentServiceHost(new string[0]));
                    s.WhenStarted(h => h.Start());
                    s.WhenStopped(h => h.Stop());
                });
                x.SetServiceName("FabrikamDocumentService");
                x.SetDisplayName("Fabrikam Housing Document Service");
                x.SetDescription("Document store, scanned post intake and full text search for Fabrikam Housing.");
                x.RunAsNetworkService();
                x.StartAutomaticallyDelayed();
                x.EnableServiceRecovery(r =>
                {
                    r.RestartService(1);
                    r.RestartService(5);
                    r.RestartService(15);
                    r.SetResetPeriod(1);
                });
            });
            NLog.LogManager.Shutdown();
            return (int)exitCode;
        }
    }
}

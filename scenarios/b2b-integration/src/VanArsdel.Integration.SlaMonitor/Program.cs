using System;
using System.Linq;
using System.ServiceProcess;
using log4net;

namespace VanArsdel.Integration.SlaMonitor
{
    internal static class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        /// <summary>
        /// Runs as the Windows service "VanArsdel B2B SLA Monitor" (installed with InstallUtil). "/console" runs the
        /// loop in the foreground, "/once" runs one check and exits (both used when diagnosing on the server).
        /// </summary>
        private static int Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();
            var once = args.Any(a => string.Equals(a, "/once", StringComparison.OrdinalIgnoreCase));
            var console = once || args.Any(a => string.Equals(a, "/console", StringComparison.OrdinalIgnoreCase)) || Environment.UserInteractive;

            if (!console)
            {
                ServiceBase.Run(new SlaMonitorService());
                return 0;
            }

            using (var container = ContainerConfig.Build())
            {
                var monitor = (MonitorLoop)container.Resolve(typeof(MonitorLoop), null);
                if (once)
                {
                    var outcome = monitor.RunOnce();
                    Console.WriteLine(outcome);
                    return outcome.Failed ? 1 : 0;
                }

                Console.WriteLine("SLA monitor running in the console; press Enter to stop.");
                monitor.Start();
                Console.ReadLine();
                monitor.Stop();
            }

            Log.Info("Console run finished");
            return 0;
        }
    }
}

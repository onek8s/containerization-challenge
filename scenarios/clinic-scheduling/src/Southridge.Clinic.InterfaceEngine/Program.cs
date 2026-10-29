using System;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using log4net;
using log4net.Config;

namespace Southridge.Clinic.InterfaceEngine
{
    internal static class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        /// <summary>
        /// Runs as a Windows service. "/console" runs in the foreground until Enter or Ctrl+C;
        /// "/once" sends each outbox once and exits (used after an outage).
        /// </summary>
        private static int Main(string[] args)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            XmlConfigurator.ConfigureAndWatch(new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log4net.config")));
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Fatal("Unhandled exception", e.ExceptionObject as Exception);

            bool console = args.Any(a => a.Equals("/console", StringComparison.OrdinalIgnoreCase) || a.Equals("-console", StringComparison.OrdinalIgnoreCase));
            bool once = args.Any(a => a.Equals("/once", StringComparison.OrdinalIgnoreCase));
            if (!console && !once)
            {
                ServiceBase.Run(new ServiceBase[] { new InterfaceEngineService() });
                return 0;
            }

            using (var host = new EngineHost())
            {
                if (once)
                {
                    return RunOnce();
                }

                host.Start();
                var stop = new System.Threading.ManualResetEvent(false);
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    stop.Set();
                };
                Console.WriteLine("Interface engine running. Press Ctrl+C to stop.");
                stop.WaitOne();
            }

            return 0;
        }

        private static int RunOnce()
        {
            Southridge.Clinic.Infrastructure.ClinicContainer.ApplyConfiguration(Southridge.Clinic.Infrastructure.ClinicContainer.Current);
            var repository = new Southridge.Clinic.Data.Repositories.InterfaceRepository();
            int total = 0;
            foreach (SenderElement config in EngineSection.Current.Senders)
            {
                if (!config.Enabled)
                {
                    continue;
                }

                int sent = new OutboundSender(config, repository, TimeSpan.FromSeconds(5)).RunOnce();
                Console.WriteLine("{0}: {1} sent", config.Partner, sent);
                total += sent;
            }

            Console.WriteLine("Total: {0}", total);
            return 0;
        }
    }
}

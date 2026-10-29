using System;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using log4net;
using Lucerne.ProjectSites.Sync;
using Unity;

namespace Lucerne.ProjectSites.SyncService
{
    /// <summary>
    /// LucerneProjectSync.exe            run as the Windows service (installed with InstallUtil)
    /// LucerneProjectSync.exe /console   run the schedules in the console
    /// LucerneProjectSync.exe /once [Job] run all jobs (or one) once and exit
    /// </summary>
    internal static class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        private static int Main(string[] args)
        {
            log4net.Config.XmlConfigurator.ConfigureAndWatch(new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log4net.config")));
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Fatal("Unhandled exception", e.ExceptionObject as Exception);

            var mode = args.FirstOrDefault()?.TrimStart('/', '-').ToLowerInvariant();
            if (mode == null && !Environment.UserInteractive)
            {
                ServiceBase.Run(new SyncWindowsService());
                return 0;
            }

            var container = ContainerConfig.Build(ServiceSettings.FromConfig());
            if (mode == "once")
            {
                var filter = args.Length > 1 ? args[1] : null;
                var runs = container.Resolve<SyncEngine>().RunAll(name => filter == null || string.Equals(name, filter, StringComparison.OrdinalIgnoreCase));
                return runs.All(r => r.Succeeded) ? 0 : 1;
            }

            using (var scheduler = new JobScheduler(container, container.Resolve<ServiceSettings>()))
            {
                scheduler.Start();
                Console.WriteLine("Running. Press Enter to stop.");
                Console.ReadLine();
            }

            return 0;
        }
    }

    public class SyncWindowsService : ServiceBase
    {
        public const string Name = "LucerneProjectSync";
        private JobScheduler _scheduler;

        public SyncWindowsService()
        {
            ServiceName = Name;
            CanStop = true;
            CanShutdown = true;
        }

        protected override void OnStart(string[] args)
        {
            var container = ContainerConfig.Build(ServiceSettings.FromConfig());
            _scheduler = new JobScheduler(container, container.Resolve<ServiceSettings>());
            _scheduler.Start();
        }

        protected override void OnStop()
        {
            _scheduler?.Dispose();
            _scheduler = null;
        }

        protected override void OnShutdown()
        {
            OnStop();
        }
    }
}

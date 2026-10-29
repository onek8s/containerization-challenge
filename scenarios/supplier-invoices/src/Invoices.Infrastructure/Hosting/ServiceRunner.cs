using System;
using System.ServiceProcess;
using System.Threading;
using Wingtip.Invoices.Infrastructure.Diagnostics;

namespace Wingtip.Invoices.Infrastructure.Hosting
{
    /// <summary>Common entry point of the hub Windows services.</summary>
    public static class ServiceRunner
    {
        public static int Run(string[] args, string eventSource, Func<HubServiceBase> create)
        {
            HubLog.Initialize(eventSource);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                HubLog.Error(HubLog.Events.Unhandled, e.ExceptionObject as Exception ?? new Exception("Unknown error"), "Unhandled exception");

            var command = args.Length > 0 ? args[0].TrimStart('-', '/').ToLowerInvariant() : null;
            switch (command)
            {
                case "install-diagnostics":
                    HubLog.Install(eventSource);
                    HubCounters.Install();
                    Console.WriteLine("Event source '{0}' and performance counters '{1}' installed.", eventSource, HubCounters.Category);
                    return 0;

                case "console":
                    using (var service = create())
                    {
                        Console.WriteLine("Running {0}. Press Enter to stop.", service.ServiceName);
                        service.StartInteractive(args);
                        Console.ReadLine();
                        service.StopInteractive();
                    }
                    return 0;

                case null:
                    if (Environment.UserInteractive)
                    {
                        Console.WriteLine("Usage: {0} [console | install-diagnostics]", AppDomain.CurrentDomain.FriendlyName);
                        return 1;
                    }
                    ServiceBase.Run(create());
                    return 0;

                default:
                    return -1;
            }
        }
    }

    public abstract class HubServiceBase : ServiceBase
    {
        private readonly ManualResetEvent stopping = new ManualResetEvent(false);

        protected WaitHandle Stopping
        {
            get { return stopping; }
        }

        protected bool IsStopping
        {
            get { return stopping.WaitOne(0); }
        }

        public void StartInteractive(string[] args)
        {
            OnStart(args);
        }

        public void StopInteractive()
        {
            OnStop();
        }

        protected sealed override void OnStart(string[] args)
        {
            stopping.Reset();
            StartWork();
            HubLog.Info(HubLog.Events.Started, "{0} {1} started.", ServiceName, GetType().Assembly.GetName().Version);
        }

        protected sealed override void OnStop()
        {
            RequestAdditionalTime(60000);
            stopping.Set();
            StopWork();
            HubLog.Info(HubLog.Events.Stopped, "{0} stopped.", ServiceName);
        }

        protected abstract void StartWork();

        protected abstract void StopWork();

        /// <summary>Starts a background loop that runs until the service stops.</summary>
        protected Thread StartLoop(string name, TimeSpan interval, Action work)
        {
            var thread = new Thread(() =>
            {
                while (!IsStopping)
                {
                    try
                    {
                        work();
                    }
                    catch (Exception ex)
                    {
                        HubLog.Error(HubLog.Events.Unhandled, ex, "{0} failed.", name);
                    }
                    if (stopping.WaitOne(interval)) break;
                }
            })
            {
                Name = name,
                IsBackground = true
            };
            thread.Start();
            return thread;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) stopping.Dispose();
            base.Dispose(disposing);
        }
    }
}

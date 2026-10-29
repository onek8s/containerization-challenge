using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using Northwind.Inspections.Infrastructure.Configuration;
using Northwind.Inspections.Infrastructure.Data;
using Northwind.Inspections.Infrastructure.Diagnostics;
using Northwind.Inspections.Infrastructure.Imaging;

namespace Northwind.Inspections.Intake
{
    /// <summary>
    /// Windows service "NorthwindInspectionsIntake". Imports photos that the field camera sync and partner adjusters
    /// drop into the Intake folder on the photo share. A FileSystemWatcher triggers a scan; a timer rescans in case
    /// change notifications from the file server are lost.
    /// </summary>
    public class IntakeService : ServiceBase
    {
        public const string Name = "NorthwindInspectionsIntake";

        private readonly object gate = new object();
        private Timer timer;
        private FileSystemWatcher watcher;
        private IntakeProcessor processor;
        private TimeSpan poll;

        public IntakeService()
        {
            ServiceName = Name;
            CanStop = true;
            CanShutdown = true;
            CanPauseAndContinue = true;
            AutoLog = false;
        }

        public void StartInteractive()
        {
            OnStart(new string[0]);
        }

        public void StopInteractive()
        {
            OnStop();
        }

        protected override void OnStart(string[] args)
        {
            InspectionsLog.UseSource(InspectionsLog.JobsSource);
            var settings = InspectionSettings.Current;
            var db = new Db(settings.ConnectionString);
            var store = new PhotoStore(settings, db);
            var intake = Path.Combine(settings.PhotoShare, ConfigurationManager.AppSettings["IntakeFolder"] ?? "Intake");
            poll = TimeSpan.FromSeconds(int.Parse(ConfigurationManager.AppSettings["PollSeconds"] ?? "60"));
            var settle = TimeSpan.FromSeconds(int.Parse(ConfigurationManager.AppSettings["SettleSeconds"] ?? "20"));

            processor = new IntakeProcessor(intake, settle, store, new InspectionRepository(db));
            store.Share.Run(() => processor.EnsureFolders());

            timer = new Timer(_ => Scan(), null, TimeSpan.FromSeconds(5), poll);

            // The watcher handle is opened under the share account so that the file server accepts it.
            store.Share.Run(() =>
            {
                watcher = new FileSystemWatcher(intake) { IncludeSubdirectories = false, InternalBufferSize = 65536 };
                watcher.Created += (s, e) => timer.Change(settle, poll);
                watcher.Renamed += (s, e) => timer.Change(settle, poll);
                watcher.Error += (s, e) => Trace.TraceWarning("Intake watcher error: {0}", e.GetException().Message);
                watcher.EnableRaisingEvents = true;
            });

            InspectionsLog.Info(EventId.Started, "Inspections intake service started, watching {0} as {1}.", intake, settings.ShareUser ?? Environment.UserName);
        }

        protected override void OnStop()
        {
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            if (timer != null)
            {
                using (var done = new ManualResetEvent(false))
                {
                    timer.Dispose(done);
                    done.WaitOne(TimeSpan.FromSeconds(30));
                }
            }
            lock (gate)
            {
                Trace.TraceInformation("Inspections intake service stopped.");
            }
        }

        protected override void OnPause()
        {
            timer.Change(Timeout.Infinite, Timeout.Infinite);
            watcher.EnableRaisingEvents = false;
        }

        protected override void OnContinue()
        {
            watcher.EnableRaisingEvents = true;
            timer.Change(TimeSpan.Zero, poll);
        }

        protected override void OnShutdown()
        {
            OnStop();
        }

        private void Scan()
        {
            if (!Monitor.TryEnter(gate))
            {
                return;
            }
            try
            {
                processor.ScanOnce();
            }
            catch (Exception ex)
            {
                InspectionsLog.Error(EventId.ShareFailure, ex, "Intake scan failed.");
            }
            finally
            {
                Monitor.Exit(gate);
            }
        }
    }
}

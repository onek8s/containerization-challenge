using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Coho.Billing.Data;
using Coho.Billing.Engine.Control;

namespace Coho.Billing.Engine
{
    public class EngineStatus
    {
        public string Version { get; set; }
        public string Machine { get; set; }
        public DateTime StartedAt { get; set; }
        public bool Paused { get; set; }
        public string PausedBy { get; set; }
        public bool Busy { get; set; }
        public string CurrentWork { get; set; }
        public DateTime? LastCycleAt { get; set; }
        public DateTime? LastImportAt { get; set; }
        public string LastError { get; set; }
    }

    /// <summary>
    /// Work loop of the billing engine. One pass imports new read files, opens runs for cycles
    /// that reached their cut-off, rates and releases runs, and writes requested reprints.
    /// </summary>
    public sealed class BillingEngine : IDisposable
    {
        private readonly EngineSettings _settings;
        private readonly BillingDb _db;
        private readonly ReadImporter _importer;
        private readonly RunProcessor _runs;
        private readonly EngineStatus _status;
        private readonly object _sync = new object();
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private ControlHost _control;
        private Thread _thread;
        private volatile bool _stopping;

        public BillingEngine(EngineSettings settings)
        {
            _settings = settings;
            _db = BillingDb.FromConfig();
            _importer = new ReadImporter(settings, _db);
            _runs = new RunProcessor(settings, _db);
            _status = new EngineStatus
            {
                Version = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                Machine = Environment.MachineName,
                StartedAt = DateTime.Now
            };
        }

        public BillingDb Database
        {
            get { return _db; }
        }

        public EngineSettings Settings
        {
            get { return _settings; }
        }

        public RunProcessor Runs
        {
            get { return _runs; }
        }

        public void Start()
        {
            _control = ControlHost.Start(this);
            _thread = new Thread(Loop) { Name = "Billing engine", IsBackground = true };
            _thread.Start();
            EngineLog.Event(EventLogEntryType.Information, 100, string.Format("Meter Billing engine {0} started. Inbox {1}, control {2}", _status.Version, _settings.ReadInbox, _settings.ControlUrl));
        }

        public void Stop()
        {
            _stopping = true;
            _wake.Set();
            if (_thread != null && !_thread.Join(TimeSpan.FromMinutes(2)))
            {
                EngineLog.Warning("Work loop did not stop within two minutes ({0})", _status.CurrentWork);
            }

            if (_control != null)
            {
                _control.Dispose();
                _control = null;
            }

            EngineLog.Event(EventLogEntryType.Information, 101, "Meter Billing engine stopped");
        }

        public void Pause(string by)
        {
            lock (_sync)
            {
                _status.Paused = true;
                _status.PausedBy = by;
            }

            EngineLog.Warning("Engine paused by {0}", by);
        }

        public void Continue(string by)
        {
            lock (_sync)
            {
                _status.Paused = false;
                _status.PausedBy = null;
            }

            EngineLog.Info("Engine continued by {0}", by);
            _wake.Set();
        }

        /// <summary>Starts a pass now instead of waiting for the interval.</summary>
        public void Wake()
        {
            _wake.Set();
        }

        public EngineStatus GetStatus()
        {
            lock (_sync)
            {
                return new EngineStatus
                {
                    Version = _status.Version,
                    Machine = _status.Machine,
                    StartedAt = _status.StartedAt,
                    Paused = _status.Paused,
                    PausedBy = _status.PausedBy,
                    Busy = _status.Busy,
                    CurrentWork = _status.CurrentWork,
                    LastCycleAt = _status.LastCycleAt,
                    LastImportAt = _status.LastImportAt,
                    LastError = _status.LastError
                };
            }
        }

        private void Loop()
        {
            while (!_stopping)
            {
                bool paused;
                lock (_sync)
                {
                    paused = _status.Paused;
                }

                if (!paused)
                {
                    RunPass();
                }

                _wake.WaitOne(_settings.WorkInterval);
            }
        }

        private void RunPass()
        {
            SetBusy(true, null);
            try
            {
                Step("import", () =>
                {
                    if (_importer.ImportPending() > 0)
                    {
                        lock (_sync) _status.LastImportAt = DateTime.Now;
                    }
                });
                Step("open runs", () => _runs.OpenDueRuns(DateTime.Today));
                Step("process runs", () => _runs.ProcessRuns(() => _stopping));
                Step("reprints", () => _runs.ProcessReprints());

                lock (_sync)
                {
                    _status.LastCycleAt = DateTime.Now;
                }
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private void Step(string name, Action action)
        {
            if (_stopping)
            {
                return;
            }

            SetBusy(true, name);
            try
            {
                action();
            }
            catch (Exception ex)
            {
                lock (_sync)
                {
                    _status.LastError = DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " " + name + ": " + ex.Message;
                }

                EngineLog.Error(ex, "Work step '{0}' failed", name);
            }
        }

        private void SetBusy(bool busy, string work)
        {
            lock (_sync)
            {
                _status.Busy = busy;
                _status.CurrentWork = work;
            }
        }

        public void Dispose()
        {
            _wake.Dispose();
        }
    }
}

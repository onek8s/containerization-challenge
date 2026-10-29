using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using log4net;
using VanArsdel.Integration.Data;
using VanArsdel.Integration.Domain.Sla;
using VanArsdel.Integration.Monitoring;

namespace VanArsdel.Integration.SlaMonitor
{
    public class RunOutcome
    {
        public int Alerts { get; set; }
        public int Notified { get; set; }
        public int Cleared { get; set; }
        public int Suppressed { get; set; }
        public int MailsSent { get; set; }
        public List<string> Unavailable { get; } = new List<string>();
        public bool Failed { get; set; }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "alerts {0}, notified {1}, cleared {2}, suppressed {3}, mails {4}, sources unavailable {5}{6}",
                Alerts, Notified, Cleared, Suppressed, MailsSent, Unavailable.Count, Failed ? ", FAILED" : string.Empty);
        }
    }

    /// <summary>The check loop: snapshot, evaluate, suppress repeats, e-mail, log.</summary>
    public class MonitorLoop
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MonitorLoop));
        private readonly MonitorSettings _settings;
        private readonly SnapshotCollector _collector;
        private readonly MonitoringRepository _monitoring;
        private readonly IAlertStateStore _state;
        private readonly IMailSender _mail;
        private readonly object _gate = new object();
        private Timer _timer;
        private bool _paused;

        public MonitorLoop(MonitorSettings settings, SnapshotCollector collector, MonitoringRepository monitoring, IAlertStateStore state, IMailSender mail)
        {
            _settings = settings;
            _collector = collector;
            _monitoring = monitoring;
            _state = state;
            _mail = mail;
        }

        public Func<DateTime> Clock { get; set; } = () => DateTime.UtcNow;

        public void Start()
        {
            _timer = new Timer(_ => Tick(), null, TimeSpan.FromSeconds(30), _settings.Interval);
            Log.InfoFormat("Monitoring every {0} minutes ({1})", _settings.Interval.TotalMinutes, _settings.Environment);
        }

        public void Stop()
        {
            if (_timer != null)
            {
                using (var done = new ManualResetEvent(false))
                {
                    _timer.Dispose(done);
                    done.WaitOne(TimeSpan.FromSeconds(60));
                }

                _timer = null;
            }

            // wait for a running check
            lock (_gate)
            {
            }
        }

        public void Pause()
        {
            _paused = true;
            Log.Info("Paused");
        }

        public void Resume()
        {
            _paused = false;
            Log.Info("Resumed");
        }

        public RunOutcome RunOnce()
        {
            lock (_gate)
            {
                var outcome = new RunOutcome();
                var now = Clock();
                try
                {
                    var snapshot = _collector.Collect(now, outcome.Unavailable);
                    var calendar = Calendar(now);
                    var alerts = new SlaEvaluator(calendar).Evaluate(snapshot, _monitoring.SlaDefinitions().Where(d => d.Enabled)).ToList();
                    alerts.AddRange(SourceAlerts(outcome.Unavailable, now));
                    outcome.Alerts = alerts.Count;

                    var decision = new AlertSuppressor(_state, _settings.RepeatAfter, _settings.EscalateAfter).Decide(alerts, now);
                    outcome.Notified = decision.Notify.Count;
                    outcome.Cleared = decision.Cleared.Count;
                    outcome.Suppressed = decision.Suppressed;

                    var groups = new Dictionary<string, string>(_monitoring.NotifyGroups(), StringComparer.OrdinalIgnoreCase);
                    if (!string.IsNullOrEmpty(_settings.FallbackRecipients) && !groups.ContainsKey("default"))
                    {
                        groups["default"] = _settings.FallbackRecipients;
                    }

                    foreach (var mail in AlertFormatter.Format(decision, groups, _settings.Environment, _settings.OpsPortalUrl))
                    {
                        try
                        {
                            _mail.Send(mail);
                            outcome.MailsSent++;
                        }
                        catch (Exception ex)
                        {
                            Log.Error("Could not send alert mail to " + mail.To, ex);
                        }
                    }

                    foreach (var alert in decision.Notify)
                    {
                        _monitoring.LogAlert(alert);
                    }

                    Heartbeat(now, outcome);
                }
                catch (Exception ex)
                {
                    outcome.Failed = true;
                    Log.Error("Monitoring run failed", ex);
                }

                Log.Info(outcome.ToString());
                return outcome;
            }
        }

        internal static IEnumerable<Alert> SourceAlerts(IEnumerable<string> unavailable, DateTime now)
        {
            foreach (var source in unavailable)
            {
                var name = source.Split(':')[0];
                yield return new Alert
                {
                    Key = "SOURCE|" + name,
                    SlaCode = "MON-SOURCE",
                    Kind = SlaKind.HostInstanceDown,
                    Severity = AlertSeverity.Warning,
                    Subject = "Monitoring cannot read " + name,
                    Detail = source,
                    RaisedUtc = now,
                    NotifyGroup = "integration"
                };
            }
        }

        private BusinessCalendar Calendar(DateTime nowUtc)
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(_settings.TimeZoneId);
            var offset = (int)zone.GetUtcOffset(nowUtc).TotalMinutes;
            var holidays = _monitoring.Holidays(nowUtc.Year).Concat(_monitoring.Holidays(nowUtc.Year + 1));
            return new BusinessCalendar(_settings.BusinessOpen, _settings.BusinessClose, holidays, offset);
        }

        /// <summary>The ops team's server checks look at this file's age to see that the monitor itself is alive.</summary>
        private void Heartbeat(DateTime now, RunOutcome outcome)
        {
            if (string.IsNullOrEmpty(_settings.HeartbeatFile))
            {
                return;
            }

            try
            {
                File.WriteAllText(_settings.HeartbeatFile, now.ToString("o", CultureInfo.InvariantCulture) + " " + outcome);
            }
            catch (IOException ex)
            {
                Log.Warn("Heartbeat file not written", ex);
            }
        }

        private void Tick()
        {
            if (_paused)
            {
                return;
            }

            if (!Monitor.TryEnter(_gate))
            {
                Log.Warn("Previous check still running; skipping this interval");
                return;
            }

            try
            {
                RunOnce();
            }
            finally
            {
                Monitor.Exit(_gate);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using VanArsdel.Integration.Domain.Sla;

namespace VanArsdel.Integration.Monitoring
{
    public class AlertState
    {
        public string Key { get; set; }
        public DateTime FirstRaisedUtc { get; set; }
        public DateTime LastNotifiedUtc { get; set; }
        public int NotifyCount { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Subject { get; set; }
        public string NotifyGroup { get; set; }
    }

    public interface IAlertStateStore
    {
        IDictionary<string, AlertState> LoadOpen();
        void Save(AlertState state);
        void Close(string key, DateTime clearedUtc);
    }

    public class AlertDecision
    {
        public List<Alert> Notify { get; } = new List<Alert>();
        public List<AlertState> Cleared { get; } = new List<AlertState>();
        public int Suppressed { get; set; }
    }

    /// <summary>
    /// Keeps the on-call mailbox usable: an open alert is repeated only after the repeat interval, is raised one
    /// severity level after the escalation count, and a "cleared" notice goes out when the condition is gone.
    /// </summary>
    public class AlertSuppressor
    {
        private readonly IAlertStateStore _store;
        private readonly TimeSpan _repeatAfter;
        private readonly int _escalateAfter;

        public AlertSuppressor(IAlertStateStore store, TimeSpan repeatAfter, int escalateAfter)
        {
            _store = store;
            _repeatAfter = repeatAfter;
            _escalateAfter = escalateAfter;
        }

        public AlertDecision Decide(IEnumerable<Alert> current, DateTime nowUtc)
        {
            var decision = new AlertDecision();
            var open = _store.LoadOpen();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var alert in current)
            {
                if (!seen.Add(alert.Key))
                {
                    continue;
                }

                AlertState state;
                if (!open.TryGetValue(alert.Key, out state))
                {
                    state = new AlertState
                    {
                        Key = alert.Key,
                        FirstRaisedUtc = nowUtc,
                        LastNotifiedUtc = nowUtc,
                        NotifyCount = 1,
                        Severity = alert.Severity,
                        Subject = alert.Subject,
                        NotifyGroup = alert.NotifyGroup
                    };
                    _store.Save(state);
                    decision.Notify.Add(alert);
                    continue;
                }

                if (nowUtc - state.LastNotifiedUtc < _repeatAfter)
                {
                    decision.Suppressed++;
                    continue;
                }

                state.NotifyCount++;
                state.LastNotifiedUtc = nowUtc;
                if (_escalateAfter > 0 && state.NotifyCount > _escalateAfter && state.Severity < AlertSeverity.Critical)
                {
                    state.Severity = state.Severity + 1;
                }

                alert.Severity = state.Severity;
                alert.Subject = "(repeat " + state.NotifyCount + ") " + alert.Subject;
                _store.Save(state);
                decision.Notify.Add(alert);
            }

            foreach (var stale in open.Values.Where(s => !seen.Contains(s.Key)).ToList())
            {
                _store.Close(stale.Key, nowUtc);
                decision.Cleared.Add(stale);
            }

            return decision;
        }
    }

    public class InMemoryAlertStateStore : IAlertStateStore
    {
        private readonly Dictionary<string, AlertState> _open = new Dictionary<string, AlertState>(StringComparer.Ordinal);

        public List<Tuple<string, DateTime>> ClosedLog { get; } = new List<Tuple<string, DateTime>>();

        public IDictionary<string, AlertState> LoadOpen()
        {
            return new Dictionary<string, AlertState>(_open, StringComparer.Ordinal);
        }

        public void Save(AlertState state)
        {
            _open[state.Key] = state;
        }

        public void Close(string key, DateTime clearedUtc)
        {
            _open.Remove(key);
            ClosedLog.Add(Tuple.Create(key, clearedUtc));
        }
    }
}

using System;
using System.Linq;
using System.Threading;
using Woodgrove.Statements.Core.Model;
using Woodgrove.Statements.Core.Scheduling;
using Woodgrove.Statements.Data;

namespace Woodgrove.Statements.Distribution
{
    /// <summary>
    /// Poll loop: schedules new subscriptions, claims due subscriptions (lease in stm.Subscription, so a second distribution
    /// host would not run the same subscription twice), runs them one after the other and resends failed deliveries.
    /// </summary>
    public class DistributionWorker
    {
        readonly DistributionSettings _settings;
        readonly SubscriptionRepository _subscriptions;
        readonly DocumentRepository _documents;
        readonly SubscriptionExecutor _executor;
        readonly ManualResetEvent _stop = new ManualResetEvent(false);
        Thread _thread;

        public DistributionWorker(DistributionSettings settings, Db db)
        {
            _settings = settings;
            _subscriptions = new SubscriptionRepository(db);
            _documents = new DocumentRepository(db);
            _executor = new SubscriptionExecutor(settings, db);
        }

        public SubscriptionExecutor Executor
        {
            get { return _executor; }
        }

        public void Start()
        {
            _stop.Reset();
            _thread = new Thread(Loop) { Name = "Distribution", IsBackground = true };
            _thread.Start();
        }

        public void Stop(TimeSpan wait)
        {
            _stop.Set();
            if (_thread != null && !_thread.Join(wait))
                Log.Warn("Distribution thread did not stop within {0}; a run is still in progress and its claim will expire.", wait);
        }

        void Loop()
        {
            Log.Info("Distribution worker started on {0}, polling every {1} s", _settings.HostName, _settings.PollSeconds);
            do
            {
                try
                {
                    PollOnce(DateTime.Now);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Poll failed");
                }
            }
            while (!_stop.WaitOne(TimeSpan.FromSeconds(_settings.PollSeconds)));
            Log.Info("Distribution worker stopped");
        }

        public void PollOnce(DateTime now)
        {
            var holidays = _documents.Holidays();
            foreach (var s in _subscriptions.WithoutNextRun())
            {
                Schedule schedule;
                string error;
                if (!Schedule.TryParse(s.Schedule, out schedule, out error))
                {
                    Log.Warn("Subscription {0} '{1}' has an invalid schedule '{2}': {3}", s.SubscriptionId, s.Name, s.Schedule, error);
                    continue;
                }
                var next = schedule.NextRun(now, holidays);
                _subscriptions.SetNextRun(s.SubscriptionId, next);
                Log.Info("Subscription {0} '{1}' scheduled for {2:yyyy-MM-dd HH:mm}", s.SubscriptionId, s.Name, next);
            }

            foreach (var s in _subscriptions.ClaimDue(_settings.HostName, now, _settings.LeaseMinutes, _settings.MaxSubscriptionsPerPoll))
            {
                if (_stop.WaitOne(0)) break;
                var outcome = _executor.Execute(s, "schedule", DateTime.Now);
                DateTime? next = null;
                Schedule schedule;
                string error;
                if (Schedule.TryParse(s.Schedule, out schedule, out error))
                    next = schedule.NextRun(DateTime.Now > (s.NextRunAt ?? now) ? DateTime.Now : s.NextRunAt.Value, holidays);
                _subscriptions.Complete(s.SubscriptionId, _settings.HostName, DateTime.Now, outcome.Status, next);
            }

            _executor.ProcessRetries(DateTime.Now);
        }

        public RunOutcome RunNow(int subscriptionId, string runBy)
        {
            var s = _subscriptions.Get(subscriptionId);
            if (s == null) throw new ArgumentException("Subscription " + subscriptionId + " not found.");
            return _executor.Execute(s, runBy, DateTime.Now);
        }
    }
}

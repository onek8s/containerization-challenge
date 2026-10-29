using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using log4net;
using Lucerne.ProjectSites.Sync;
using Unity;

namespace Lucerne.ProjectSites.SyncService
{
    /// <summary>
    /// One timer per job. A job never overlaps with itself; a run that takes longer than the interval simply
    /// delays the next one.
    /// </summary>
    public sealed class JobScheduler : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(JobScheduler));
        private readonly IUnityContainer _container;
        private readonly ServiceSettings _settings;
        private readonly List<Timer> _timers = new List<Timer>();
        private readonly HashSet<string> _running = new HashSet<string>();
        private volatile bool _stopping;

        public JobScheduler(IUnityContainer container, ServiceSettings settings)
        {
            _container = container;
            _settings = settings;
        }

        public void Start()
        {
            var delay = TimeSpan.FromSeconds(15);
            foreach (var entry in _settings.Intervals.Where(i => i.Value > TimeSpan.Zero))
            {
                var name = entry.Key;
                var interval = entry.Value;
                _timers.Add(new Timer(_ => Run(name), null, delay, interval));
                Log.InfoFormat("{0} scheduled every {1}", name, interval);
                delay = delay.Add(TimeSpan.FromSeconds(20));
            }
        }

        private void Run(string name)
        {
            lock (_running)
            {
                if (_stopping || !_running.Add(name))
                {
                    return;
                }
            }

            try
            {
                var engine = _container.Resolve<SyncEngine>();
                var job = _container.Resolve<ISyncJob>(name);
                engine.RunOne(job);
            }
            catch (Exception ex)
            {
                Log.Error(name + " could not be started", ex);
            }
            finally
            {
                lock (_running)
                {
                    _running.Remove(name);
                }
            }
        }

        public void Dispose()
        {
            _stopping = true;
            foreach (var t in _timers)
            {
                t.Dispose();
            }

            var waited = 0;
            while (waited < 60000)
            {
                lock (_running)
                {
                    if (_running.Count == 0)
                    {
                        break;
                    }
                }

                Thread.Sleep(500);
                waited += 500;
            }
        }
    }
}

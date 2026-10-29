using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.InterfaceEngine
{
    /// <summary>Starts and stops the configured listeners and senders. Used by the service and by console mode.</summary>
    public class EngineHost : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(EngineHost));

        private readonly List<MllpListener> _listeners = new List<MllpListener>();
        private readonly List<OutboundSender> _senders = new List<OutboundSender>();
        private System.Threading.Timer _heartbeat;

        public IList<OutboundSender> Senders
        {
            get { return _senders; }
        }

        public void Start()
        {
            ClinicContainer.ApplyConfiguration(ClinicContainer.Current);
            EngineSection config = EngineSection.Current;
            var repository = new InterfaceRepository();
            var processor = new InboundProcessor(repository);
            foreach (ListenerElement l in config.Listeners.Cast<ListenerElement>().Where(l => l.Enabled))
            {
                var listener = new MllpListener(l, processor);
                listener.Start();
                _listeners.Add(listener);
            }

            foreach (SenderElement s in config.Senders.Cast<SenderElement>().Where(s => s.Enabled))
            {
                var sender = new OutboundSender(s, repository, TimeSpan.FromSeconds(config.PollSeconds));
                sender.Start();
                _senders.Add(sender);
            }

            _heartbeat = new System.Threading.Timer(Heartbeat, null, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));
            Log.InfoFormat("Interface engine {0} started: {1} listeners, {2} senders", typeof(EngineHost).Assembly.GetName().Version, _listeners.Count, _senders.Count);
            ClinicLog.Info(LogCategory.Interface, "Interface engine started on {0}.", Environment.MachineName);
        }

        public void Stop()
        {
            if (_heartbeat != null)
            {
                _heartbeat.Dispose();
                _heartbeat = null;
            }

            foreach (MllpListener listener in _listeners)
            {
                listener.Stop();
            }

            foreach (OutboundSender sender in _senders)
            {
                sender.Stop();
            }

            _listeners.Clear();
            _senders.Clear();
            Log.Info("Interface engine stopped");
        }

        public void Dispose()
        {
            Stop();
        }

        private void Heartbeat(object state)
        {
            Log.InfoFormat("Heartbeat: {0}", string.Join(", ", _senders.Select(s => s.Partner + " sent " + s.SentCount + " failed " + s.FailedCount)
                .Concat(_listeners.Select(l => l.Partner + " connections " + l.ActiveConnections)).ToArray()));
        }
    }
}

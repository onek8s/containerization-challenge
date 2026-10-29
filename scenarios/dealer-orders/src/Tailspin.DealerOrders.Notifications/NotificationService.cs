using System;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Tailspin.DealerOrders.Infrastructure;
using Tailspin.DealerOrders.Infrastructure.Composition;
using Unity;
using Unity.Lifetime;

namespace Tailspin.DealerOrders.Notifications
{
    public class NotificationService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(NotificationService));

        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private IUnityContainer _container;
        private PortalPushClient _push;
        private Task _loop;

        public void Start()
        {
            _push = new PortalPushClient();
            _container = CoreRegistrations.Register(new UnityContainer(), () => new HierarchicalLifetimeManager());
            _container.RegisterInstance(_push);

            var interval = Settings.Time("Notifications.Interval", TimeSpan.FromSeconds(10));
            var batch = Settings.Int("Notifications.BatchSize", 50);
            _loop = Task.Factory.StartNew(() => Loop(interval, batch), TaskCreationOptions.LongRunning);
            Log.InfoFormat("Notification service started (every {0}, push {1})", interval, _push.Enabled ? "on" : "off");
        }

        private void Loop(TimeSpan interval, int batch)
        {
            var failures = 0;
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _container.CreateChildContainer())
                    {
                        var count = scope.Resolve<NotificationDispatcher>().Run(batch);
                        if (count > 0) Log.InfoFormat("{0} notifications sent", count);
                    }
                    failures = 0;
                }
                catch (Exception ex)
                {
                    failures++;
                    Log.Error($"Notification pass failed ({failures} in a row)", ex);
                }
                if (_stop.Token.WaitHandle.WaitOne(TimeSpan.FromTicks(interval.Ticks * Math.Min(10, 1 + failures)))) break;
            }
        }

        public void Stop()
        {
            _stop.Cancel();
            _loop?.Wait(TimeSpan.FromSeconds(30));
            _push?.Dispose();
            _container?.Dispose();
            Log.Info("Notification service stopped");
        }
    }
}

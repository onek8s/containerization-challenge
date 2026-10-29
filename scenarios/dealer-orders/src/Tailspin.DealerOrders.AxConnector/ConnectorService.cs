using System;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Tailspin.DealerOrders.AxIntegration;
using Tailspin.DealerOrders.Contracts.Ax;
using Tailspin.DealerOrders.Infrastructure;
using Tailspin.DealerOrders.Infrastructure.Composition;
using Tailspin.DealerOrders.Processing.Ax;
using Unity;
using Unity.Lifetime;

namespace Tailspin.DealerOrders.AxConnector
{
    /// <summary>
    /// Two loops: the command pump runs queued AX commands every few seconds; the status poller reads orders changed in AX.
    /// Each pass uses its own child container, so one EF context per unit of work.
    /// </summary>
    public class ConnectorService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ConnectorService));

        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private IUnityContainer _container;
        private AxGateways _ax;
        private Task _pump;
        private Task _poller;

        public void Start()
        {
            _ax = AxGateways.FromConfig();
            _container = CoreRegistrations.Register(new UnityContainer(), () => new HierarchicalLifetimeManager());
            _container.RegisterInstance<IAxSalesGateway>(_ax.Sales);
            _container.RegisterInstance<IAxDocumentReader>(_ax.Reader);
            _container.RegisterInstance<IAxQueryReader>(_ax.Query);

            var pumpInterval = Settings.Time("Connector.PumpInterval", TimeSpan.FromSeconds(5));
            var pollInterval = Settings.Time("Connector.StatusPollInterval", TimeSpan.FromMinutes(5));
            var batch = Settings.Int("Connector.BatchSize", 20);

            _pump = Task.Factory.StartNew(() => Loop("command pump", pumpInterval, scope => scope.Resolve<AxCommandProcessor>().RunDue(batch)),
                TaskCreationOptions.LongRunning);
            _poller = Task.Factory.StartNew(() => Loop("status poller", pollInterval, scope => scope.Resolve<ChangedOrderPoller>().Run()),
                TaskCreationOptions.LongRunning);
            Log.InfoFormat("AX connector started in {0} mode (pump {1}, status poll {2})", _ax.Mode, pumpInterval, pollInterval);
        }

        private void Loop(string name, TimeSpan interval, Func<IUnityContainer, int> work)
        {
            var failures = 0;
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    int count;
                    using (var scope = _container.CreateChildContainer())
                        count = work(scope);
                    if (count > 0) Log.DebugFormat("{0}: {1} processed", name, count);
                    failures = 0;
                }
                catch (Exception ex)
                {
                    failures++;
                    Log.Error($"{name} failed ({failures} in a row)", ex);
                }

                // Back off while the database or AX keeps failing, up to ten intervals.
                var wait = TimeSpan.FromTicks(interval.Ticks * Math.Min(10, 1 + failures * failures));
                if (_stop.Token.WaitHandle.WaitOne(wait)) break;
            }
        }

        public void Stop()
        {
            _stop.Cancel();
            Task.WaitAll(new[] { _pump, _poller }, TimeSpan.FromSeconds(60));
            _ax?.Dispose();
            _container?.Dispose();
            Log.Info("AX connector stopped");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using log4net;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using Tailspin.DealerOrders.AxIntegration;
using Tailspin.DealerOrders.Contracts.Ax;
using Tailspin.DealerOrders.Infrastructure.Composition;
using Tailspin.DealerOrders.PriceSync.Jobs;
using Unity;
using Unity.Lifetime;

namespace Tailspin.DealerOrders.PriceSync
{
    /// <summary>
    /// Quartz.NET scheduler with the in-memory job store. Jobs and triggers come from quartz_jobs.xml (XML scheduling
    /// data plugin), so schedules can be changed on the server without a new build.
    /// </summary>
    public class SyncService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SyncService));

        private IScheduler _scheduler;
        private IUnityContainer _container;
        private AxGateways _ax;

        public static readonly IDictionary<string, Type> Jobs = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            ["prices"] = typeof(PriceListJob),
            ["stock"] = typeof(StockJob),
            ["balances"] = typeof(DealerBalanceJob),
            ["rates"] = typeof(ExchangeRateJob),
            ["items"] = typeof(ItemJob),
            ["quote-expiry"] = typeof(QuoteExpiryJob),
            ["digest"] = typeof(DailyDigestJob),
            ["document-purge"] = typeof(DocumentPurgeJob)
        };

        private IUnityContainer BuildContainer()
        {
            _ax = AxGateways.FromConfig();
            var container = CoreRegistrations.Register(new UnityContainer(), () => new HierarchicalLifetimeManager());
            container.RegisterInstance<IAxSalesGateway>(_ax.Sales);
            container.RegisterInstance<IAxDocumentReader>(_ax.Reader);
            container.RegisterInstance<IAxQueryReader>(_ax.Query);
            return container;
        }

        public void Start()
        {
            _container = BuildContainer();
            var properties = new NameValueCollection
            {
                ["quartz.scheduler.instanceName"] = "DealerOrdersPriceSync",
                ["quartz.threadPool.threadCount"] = "3",
                ["quartz.jobStore.type"] = "Quartz.Simpl.RAMJobStore, Quartz",
                ["quartz.plugin.xml.type"] = "Quartz.Plugin.Xml.XMLSchedulingDataProcessorPlugin, Quartz",
                ["quartz.plugin.xml.fileNames"] = "~/quartz_jobs.xml",
                ["quartz.plugin.xml.failOnFileNotFound"] = "true",
                ["quartz.plugin.xml.scanInterval"] = "120"
            };
            _scheduler = new StdSchedulerFactory(properties).GetScheduler();
            _scheduler.JobFactory = new UnityJobFactory(_container);
            _scheduler.Start();
            Log.InfoFormat("Scheduler started in {0} mode with {1} jobs", _ax.Mode, _scheduler.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<JobKey>.AnyGroup()).Count);
        }

        public void Stop()
        {
            _scheduler?.Shutdown(waitForJobsToComplete: true);
            _ax?.Dispose();
            _container?.Dispose();
        }

        public static int RunOnce(string name)
        {
            if (!Jobs.TryGetValue(name, out var type))
            {
                Console.Error.WriteLine("Unknown job. Jobs: " + string.Join(", ", Jobs.Keys));
                return 2;
            }
            var service = new SyncService();
            service._container = service.BuildContainer();
            try
            {
                using (var scope = service._container.CreateChildContainer())
                {
                    var job = (SyncJob)scope.Resolve(type);
                    var count = job.Run();
                    Console.WriteLine($"{name}: {count}");
                }
                return 0;
            }
            catch (Exception ex)
            {
                Log.Error("Job " + name + " failed", ex);
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
            finally
            {
                service._ax.Dispose();
                service._container.Dispose();
            }
        }

        private sealed class UnityJobFactory : IJobFactory
        {
            private readonly IUnityContainer _root;
            private readonly Dictionary<IJob, IUnityContainer> _scopes = new Dictionary<IJob, IUnityContainer>();

            public UnityJobFactory(IUnityContainer root) { _root = root; }

            public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
            {
                var scope = _root.CreateChildContainer();
                var job = (IJob)scope.Resolve(bundle.JobDetail.JobType);
                lock (_scopes) _scopes[job] = scope;
                return job;
            }

            public void ReturnJob(IJob job)
            {
                IUnityContainer scope;
                lock (_scopes)
                {
                    if (!_scopes.TryGetValue(job, out scope)) return;
                    _scopes.Remove(job);
                }
                scope.Dispose();
            }
        }
    }
}

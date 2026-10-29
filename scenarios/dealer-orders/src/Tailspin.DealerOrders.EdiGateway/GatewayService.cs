using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using log4net;
using Microsoft.Owin.Hosting;
using Owin;
using Tailspin.DealerOrders.EdiGateway.As2;
using Tailspin.DealerOrders.EdiGateway.Sftp;
using Tailspin.DealerOrders.Infrastructure;
using Tailspin.DealerOrders.Infrastructure.Composition;
using Unity;
using Unity.Lifetime;

namespace Tailspin.DealerOrders.EdiGateway
{
    /// <summary>OWIN self-hosted AS2 receiver plus two loops: outbound dispatch and SFTP inbound polling.</summary>
    public class GatewayService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GatewayService));

        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private IUnityContainer _container;
        private IDisposable _web;
        private Task _outbound;
        private Task _sftp;

        public void Start()
        {
            var settings = GatewaySettings.FromConfig();
            _container = CoreRegistrations.Register(new UnityContainer(), () => new HierarchicalLifetimeManager());
            _container.RegisterInstance(settings);
            _container.RegisterInstance(new HttpClient { Timeout = settings.HttpTimeout });
            _container.RegisterType<CertificateProvider>(new ContainerControlledLifetimeManager());
            _container.RegisterType<AsyncMdnSender>(new ContainerControlledLifetimeManager());
            _container.RegisterType<As2Sender>(new HierarchicalLifetimeManager());
            if (string.IsNullOrEmpty(settings.SftpLocalRoot))
                _container.RegisterType<IFileTransport, SftpTransport>(new ContainerControlledLifetimeManager());
            else
                _container.RegisterType<IFileTransport, LocalFolderTransport>(new ContainerControlledLifetimeManager());

            // Fail at start, not on the first message, when our certificate is missing.
            Log.InfoFormat("AS2 certificate {0} ({1}), valid to {2:yyyy-MM-dd}", _container.Resolve<CertificateProvider>().Own.Subject,
                _container.Resolve<CertificateProvider>().Own.Thumbprint, _container.Resolve<CertificateProvider>().Own.NotAfter);

            _web = WebApp.Start(settings.ListenUrl, app =>
            {
                var config = new HttpConfiguration { DependencyResolver = new UnityResolver(_container.CreateChildContainer()) };
                config.MapHttpAttributeRoutes();
                config.Formatters.Remove(config.Formatters.XmlFormatter);
                config.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.LocalOnly;
                app.UseWebApi(config);
            });

            _outbound = Task.Factory.StartNew(() => Loop("outbound", settings.OutboundInterval, s => s.Resolve<OutboundDispatcher>().Run(50)),
                TaskCreationOptions.LongRunning);
            _sftp = Task.Factory.StartNew(() => Loop("sftp poll", settings.SftpPollInterval, s => s.Resolve<SftpPoller>().Run()),
                TaskCreationOptions.LongRunning);
            Log.InfoFormat("EDI gateway listening on {0}as2 (SFTP {1})", settings.ListenUrl,
                string.IsNullOrEmpty(settings.SftpLocalRoot) ? "remote" : "local folders under " + settings.SftpLocalRoot);
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
                    if (count > 0) Log.InfoFormat("{0}: {1} processed", name, count);
                    failures = 0;
                }
                catch (Exception ex)
                {
                    failures++;
                    Log.Error($"{name} failed ({failures} in a row)", ex);
                }
                var wait = TimeSpan.FromTicks(interval.Ticks * Math.Min(10, 1 + failures));
                if (_stop.Token.WaitHandle.WaitOne(wait)) break;
            }
        }

        public void Stop()
        {
            _stop.Cancel();
            _web?.Dispose();
            Task.WaitAll(new[] { _outbound, _sftp }, TimeSpan.FromSeconds(60));
            _container?.Dispose();
            Log.Info("EDI gateway stopped");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using log4net;
using Tailspin.DealerOrders.Documents.Excel;
using Tailspin.DealerOrders.Documents.Word;
using Tailspin.DealerOrders.Infrastructure;
using Tailspin.DealerOrders.Infrastructure.Composition;
using Unity;
using Unity.Lifetime;

namespace Tailspin.DealerOrders.DocumentService
{
    /// <summary>Application-wide container and client keys. WCF creates the service per call; it takes a child container.</summary>
    public static class ServiceRuntime
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ServiceRuntime));

        public static IUnityContainer Container { get; private set; }

        /// <summary>Client name by key, from "DocumentService.ClientKeys" (name=key;name=key).</summary>
        public static IDictionary<string, string> Clients { get; private set; }

        public static void Initialize()
        {
            var templates = Settings.Get("Documents.TemplateFolder", Path.Combine(CoreRegistrations.BinFolder, "templates"));
            var container = new UnityContainer();
            CoreRegistrations.Register(container, () => new HierarchicalLifetimeManager());
            container.RegisterInstance(new SalesDocumentBuilder(name => File.ReadAllBytes(Path.Combine(templates, name))));
            container.RegisterType<PriceListWorkbook>(new ContainerControlledLifetimeManager());
            container.RegisterType<DocumentModels>(new HierarchicalLifetimeManager());
            Container = container;

            Clients = (Settings.Get("DocumentService.ClientKeys") ?? "")
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split(new[] { '=' }, 2))
                .Where(p => p.Length == 2 && p[1].Trim().Length > 0)
                .ToDictionary(p => p[1].Trim(), p => p[0].Trim(), StringComparer.Ordinal);
            Log.InfoFormat("Document service started; templates in {0}; {1} clients configured", templates, Clients.Count);
        }

        public static void Shutdown()
        {
            Container?.Dispose();
        }
    }
}

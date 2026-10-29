using System;
using System.IO;
using Tailspin.DealerOrders.Configurator;
using Tailspin.DealerOrders.Data;
using Tailspin.DealerOrders.Data.Queries;
using Tailspin.DealerOrders.Data.Stores;
using Tailspin.DealerOrders.Infrastructure.Mail;
using Tailspin.DealerOrders.Processing;
using Tailspin.DealerOrders.Processing.Ax;
using Tailspin.DealerOrders.Processing.Catalog;
using Tailspin.DealerOrders.Processing.Edi;
using Tailspin.DealerOrders.Processing.Notifications;
using Tailspin.DealerOrders.Processing.Ports;
using Tailspin.DealerOrders.Processing.Sales;
using Tailspin.DealerOrders.Processing.Sync;
using Unity;
using Unity.Injection;
using Unity.Lifetime;

namespace Tailspin.DealerOrders.Infrastructure.Composition
{
    /// <summary>
    /// Unity registrations shared by the dealer portal and the Windows services. <paramref name="scope"/> creates the
    /// lifetime for the EF context and everything that uses it (per request in the portal, per unit of work in services).
    /// </summary>
    public static class CoreRegistrations
    {
        /// <summary>The bin folder of a web application, or the folder of a service executable.</summary>
        public static string BinFolder => AppDomain.CurrentDomain.RelativeSearchPath ?? AppDomain.CurrentDomain.BaseDirectory;

        public static IUnityContainer Register(IUnityContainer container, Func<LifetimeManager> scope)
        {
            var settings = Settings.Processing();
            var connections = SqlConnectionFactory.FromConfig();
            var catalogFolder = Settings.Get("Catalog.Folder", Path.Combine(BinFolder, "catalog"));
            var cache = new SqlCacheStore(connections);

            container.RegisterInstance(settings);
            container.RegisterInstance(connections);
            container.RegisterInstance<IClock>(new SystemClock(Settings.Get("Orders.FactoryTimeZone", "FLE Standard Time")));
            container.RegisterInstance<ICatalog>(new FileCatalog(catalogFolder, cache.ItemName));
            container.RegisterInstance<ICacheStore>(cache);
            container.RegisterInstance(cache);
            container.RegisterInstance<IPriceSource>(new SqlPriceSource(connections));
            container.RegisterInstance(new ConfigurationEngine());
            container.RegisterInstance(new LeadTimeCalculator());
            container.RegisterType<PriceCalculator>(new ContainerControlledLifetimeManager());
            container.RegisterType<IAxCommandQueue, SqlAxCommandQueue>(new ContainerControlledLifetimeManager());
            container.RegisterType<SqlAxCommandQueue>(new ContainerControlledLifetimeManager());
            container.RegisterType<SqlDocumentStore>(new ContainerControlledLifetimeManager());
            container.RegisterType<OrderQueries>(new ContainerControlledLifetimeManager());
            container.RegisterType<IMailSender, SmtpMailSender>(new ContainerControlledLifetimeManager());

            container.RegisterType<DealerOrdersContext>(scope(), new InjectionConstructor());
            container.RegisterType<IDealerStore, EfDealerStore>(scope());
            container.RegisterType<IQuoteStore, EfQuoteStore>(scope());
            container.RegisterType<IOrderStore, EfOrderStore>(scope());
            container.RegisterType<IApprovalStore, EfApprovalStore>(scope());
            container.RegisterType<IShipmentStore, EfShipmentStore>(scope());
            container.RegisterType<INotificationStore, EfNotificationStore>(scope());
            container.RegisterType<IEdiStore, EfEdiStore>(scope());

            container.RegisterType<NotificationComposer>(scope());
            container.RegisterType<AxCommandFactory>(scope());
            container.RegisterType<QuoteService>(scope());
            container.RegisterType<OrderService>(scope());
            container.RegisterType<ApprovalService>(scope());
            container.RegisterType<EdiOutbound>(scope());
            container.RegisterType<EdiOrderIntake>(scope());
            container.RegisterType<OrderStatusSync>(scope());
            container.RegisterType<AxDocumentMapper>(scope());
            container.RegisterType<AxCommandProcessor>(scope());
            container.RegisterType<ChangedOrderPoller>(scope());
            container.RegisterType<CacheSync>(scope());
            container.RegisterType<DigestBuilder>(scope());
            return container;
        }
    }
}

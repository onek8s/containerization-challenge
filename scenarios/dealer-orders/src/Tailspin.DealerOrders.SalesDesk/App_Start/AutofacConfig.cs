using System.Reflection;
using System.Web.Http;
using System.Web.Mvc;
using Autofac;
using Autofac.Integration.Mvc;
using Autofac.Integration.WebApi;
using Tailspin.DealerOrders.Configurator;
using Tailspin.DealerOrders.Data;
using Tailspin.DealerOrders.Data.Queries;
using Tailspin.DealerOrders.Data.Stores;
using Tailspin.DealerOrders.Infrastructure;
using Tailspin.DealerOrders.Infrastructure.Composition;
using Tailspin.DealerOrders.Infrastructure.DocumentService;
using Tailspin.DealerOrders.Processing;
using Tailspin.DealerOrders.Processing.Catalog;
using Tailspin.DealerOrders.Processing.Edi;
using Tailspin.DealerOrders.Processing.Notifications;
using Tailspin.DealerOrders.Processing.Ports;
using Tailspin.DealerOrders.Processing.Sales;
using Tailspin.DealerOrders.SalesDesk.Infrastructure;

namespace Tailspin.DealerOrders.SalesDesk
{
    /// <summary>
    /// The sales desk uses Autofac (it came from the order desk team); the registrations mirror the shared Unity
    /// registrations in CoreRegistrations, with the EF context per request.
    /// </summary>
    public static class AutofacConfig
    {
        public static void Register(HttpConfiguration api)
        {
            var builder = new ContainerBuilder();
            var settings = Settings.Processing();
            var connections = SqlConnectionFactory.FromConfig();
            var cache = new SqlCacheStore(connections);

            builder.RegisterInstance(settings);
            builder.RegisterInstance(connections);
            builder.RegisterInstance<IClock>(new SystemClock(Settings.Get("Orders.FactoryTimeZone", "FLE Standard Time")));
            builder.RegisterInstance<ICatalog>(new FileCatalog(Settings.Get("Catalog.Folder", System.IO.Path.Combine(CoreRegistrations.BinFolder, "catalog")), cache.ItemName));
            builder.RegisterInstance(cache).As<ICacheStore>().AsSelf();
            builder.RegisterInstance<IPriceSource>(new SqlPriceSource(connections));
            builder.RegisterInstance(new ConfigurationEngine());
            builder.RegisterInstance(new LeadTimeCalculator());
            builder.RegisterInstance(new DocumentServiceClient());
            builder.RegisterType<PriceCalculator>().SingleInstance();
            builder.RegisterType<SqlAxCommandQueue>().As<IAxCommandQueue>().AsSelf().SingleInstance();
            builder.RegisterType<SqlDocumentStore>().SingleInstance();
            builder.RegisterType<OrderQueries>().SingleInstance();
            builder.RegisterType<DeskQueries>().SingleInstance();
            builder.RegisterType<DeskUsers>().SingleInstance();

            builder.Register(c => new DealerOrdersContext()).InstancePerRequest();
            builder.RegisterType<EfDealerStore>().As<IDealerStore>().InstancePerRequest();
            builder.RegisterType<EfQuoteStore>().As<IQuoteStore>().InstancePerRequest();
            builder.RegisterType<EfOrderStore>().As<IOrderStore>().InstancePerRequest();
            builder.RegisterType<EfApprovalStore>().As<IApprovalStore>().InstancePerRequest();
            builder.RegisterType<EfShipmentStore>().As<IShipmentStore>().InstancePerRequest();
            builder.RegisterType<EfNotificationStore>().As<INotificationStore>().InstancePerRequest();
            builder.RegisterType<EfEdiStore>().As<IEdiStore>().InstancePerRequest();

            builder.RegisterType<NotificationComposer>().InstancePerRequest();
            builder.RegisterType<AxCommandFactory>().InstancePerRequest();
            builder.RegisterType<QuoteService>().InstancePerRequest();
            builder.RegisterType<OrderService>().InstancePerRequest();
            builder.RegisterType<ApprovalService>().InstancePerRequest();
            builder.RegisterType<EdiOutbound>().InstancePerRequest();
            builder.RegisterType<EdiOrderIntake>().InstancePerRequest();

            builder.RegisterControllers(Assembly.GetExecutingAssembly());
            builder.RegisterApiControllers(Assembly.GetExecutingAssembly());
            builder.RegisterFilterProvider();

            var container = builder.Build();
            DependencyResolver.SetResolver(new AutofacDependencyResolver(container));
            api.DependencyResolver = new AutofacWebApiDependencyResolver(container);
        }
    }
}

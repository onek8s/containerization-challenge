using System.Web.UI;
using Microsoft.Practices.Unity;
using VanArsdel.Integration.Data;
using VanArsdel.Integration.Management;
using VanArsdel.Integration.Management.Catalog;
using VanArsdel.Integration.Management.Operations;
using VanArsdel.Integration.Management.Wmi;

namespace VanArsdel.Integration.OpsPortal
{
    /// <summary>
    /// Unity container of the portal. Pages are created by ASP.NET, so OpsPage calls BuildUp in PreInit and the
    /// services arrive through [Dependency] properties.
    /// </summary>
    public static class ContainerConfig
    {
        public const string MessageBox = "BizTalkMsgBoxDb";

        public static IUnityContainer Container { get; private set; }

        public static void Configure()
        {
            var container = new UnityContainer();
            var group = BizTalkGroup.FromConfig();

            container.RegisterInstance(group);
            container.RegisterType<IntegrationDatabase>(new ContainerControlledLifetimeManager(), new InjectionConstructor(IntegrationDatabase.DefaultName));
            container.RegisterType<BizTalkDatabaseQueries>(new ContainerControlledLifetimeManager(), new InjectionFactory(c => new BizTalkDatabaseQueries(new IntegrationDatabase(MessageBox))));
            container.RegisterType<PartnerRepository>(new ContainerControlledLifetimeManager());
            container.RegisterType<ResubmitRepository>(new ContainerControlledLifetimeManager());
            container.RegisterType<MonitoringRepository>(new ContainerControlledLifetimeManager());

            container.RegisterType<IInstanceOperations, BizTalkInstanceOperations>(new ContainerControlledLifetimeManager());
            container.RegisterType<IGroupCatalog, ExplorerCatalog>(new ContainerControlledLifetimeManager());
            container.RegisterType<IHostInstances>(new ContainerControlledLifetimeManager(), new InjectionFactory(c => new WmiHostInstances(group.WmiServer)));
            container.RegisterType<ResubmitService>(new ContainerControlledLifetimeManager(), new InjectionFactory(c => ResubmitService.FromConfig(c.Resolve<IInstanceOperations>(), c.Resolve<ResubmitRepository>())));

            Container = container;
        }

        public static void BuildUp(Page page)
        {
            Container.BuildUp(page.GetType(), page);
        }

        public static void Dispose()
        {
            if (Container != null)
            {
                Container.Dispose();
                Container = null;
            }
        }
    }
}

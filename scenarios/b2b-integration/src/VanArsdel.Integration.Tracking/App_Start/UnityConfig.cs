using System.Web.Mvc;
using Microsoft.Practices.Unity;
using Unity.Mvc5;
using VanArsdel.Integration.Data;
using VanArsdel.Integration.Tracking.Security;

namespace VanArsdel.Integration.Tracking
{
    public static class UnityConfig
    {
        public const string BamDatabase = "BAMPrimaryImport";

        public static void RegisterComponents()
        {
            var container = new UnityContainer();
            IntegrationDatabase.EnsureFactory();

            // two databases: the integration database (users, interchanges) and the BAM primary import (order lifecycle)
            container.RegisterType<IntegrationDatabase>(new ContainerControlledLifetimeManager(), new InjectionConstructor(IntegrationDatabase.DefaultName));
            container.RegisterType<TrackingRepository>(new ContainerControlledLifetimeManager(), new InjectionFactory(c => new TrackingRepository(new IntegrationDatabase(BamDatabase))));
            container.RegisterType<InterchangeRepository>(new ContainerControlledLifetimeManager());
            container.RegisterType<PartnerRepository>(new ContainerControlledLifetimeManager());
            container.RegisterType<IPortalUserStore, SqlPortalUserStore>(new ContainerControlledLifetimeManager());
            container.RegisterType<PasswordHasher>(new ContainerControlledLifetimeManager());
            container.RegisterType<SignInService>(new ContainerControlledLifetimeManager());

            DependencyResolver.SetResolver(new UnityDependencyResolver(container));
        }
    }
}

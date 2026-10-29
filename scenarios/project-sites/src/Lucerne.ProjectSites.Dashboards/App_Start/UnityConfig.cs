using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web.Mvc;
using Lucerne.ProjectSites.Core;
using Lucerne.ProjectSites.Dashboards.Infrastructure;
using Lucerne.ProjectSites.Dashboards.Services;
using Lucerne.ProjectSites.Data;
using Lucerne.ProjectSites.SharePoint.Auth;
using Unity;
using Unity.Injection;
using Unity.Lifetime;

namespace Lucerne.ProjectSites.Dashboards
{
    public static class UnityConfig
    {
        public static IUnityContainer Container { get; private set; }

        public static void Register()
        {
            var container = new UnityContainer();
            var settings = AddInSettings.FromConfig();

            container.RegisterInstance(settings);
            container.RegisterInstance<IActivityLog>(new Log4NetActivityLog("Dashboards"));
            container.RegisterType<HighTrustTokenIssuer>(new ContainerControlledLifetimeManager(), new InjectionFactory(c => settings.CreateIssuer()));
            container.RegisterType<Func<ProjectSitesContext>>(new InjectionFactory(c => new Func<ProjectSitesContext>(() => new ProjectSitesContext())));
            container.RegisterType<KpiRepository>();
            container.RegisterType<PortfolioService>();

            Container = container;
            DependencyResolver.SetResolver(new UnityDependencyResolver(container));
        }
    }

    internal sealed class UnityDependencyResolver : IDependencyResolver
    {
        private readonly IUnityContainer _container;

        public UnityDependencyResolver(IUnityContainer container)
        {
            _container = container;
        }

        public object GetService(Type serviceType)
        {
            if (serviceType.IsAbstract && !_container.IsRegistered(serviceType))
            {
                return null;
            }

            try
            {
                return _container.Resolve(serviceType);
            }
            catch (Unity.Exceptions.ResolutionFailedException)
            {
                return null;
            }
        }

        public IEnumerable<object> GetServices(Type serviceType)
        {
            return _container.IsRegistered(serviceType) ? _container.ResolveAll(serviceType) : Enumerable.Empty<object>();
        }
    }
}

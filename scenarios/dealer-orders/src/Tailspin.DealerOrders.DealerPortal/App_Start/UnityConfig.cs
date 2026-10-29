using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;
using Tailspin.DealerOrders.Documents.Excel;
using Tailspin.DealerOrders.Infrastructure.Composition;
using Tailspin.DealerOrders.Infrastructure.DocumentService;
using Unity;
using Unity.Exceptions;
using Unity.Lifetime;

namespace Tailspin.DealerOrders.DealerPortal
{
    /// <summary>
    /// Unity for MVC: each request resolves from its own child container (kept in HttpContext.Items), so the EF context
    /// and the stores registered with a hierarchical lifetime live for one request and are disposed at EndRequest.
    /// </summary>
    public static class UnityConfig
    {
        private const string ScopeKey = "Tailspin.Unity.RequestScope";

        public static IUnityContainer Container { get; private set; }

        public static void Register()
        {
            var container = new UnityContainer();
            CoreRegistrations.Register(container, () => new HierarchicalLifetimeManager());
            container.RegisterInstance(new DocumentServiceClient());
            container.RegisterType<PriceListWorkbook>(new ContainerControlledLifetimeManager());
            Container = container;
            DependencyResolver.SetResolver(new RequestResolver());
        }

        public static IUnityContainer RequestScope
        {
            get
            {
                var items = HttpContext.Current?.Items;
                if (items == null) return Container;
                if (!(items[ScopeKey] is IUnityContainer scope))
                {
                    scope = Container.CreateChildContainer();
                    items[ScopeKey] = scope;
                }
                return scope;
            }
        }

        public static void EndRequest(HttpContext context)
        {
            (context.Items[ScopeKey] as IDisposable)?.Dispose();
            context.Items.Remove(ScopeKey);
        }

        private class RequestResolver : IDependencyResolver
        {
            public object GetService(Type serviceType)
            {
                // MVC asks for many optional services; only types Unity can build are created.
                if (serviceType.IsInterface || serviceType.IsAbstract)
                {
                    if (!RequestScope.IsRegistered(serviceType)) return null;
                }
                try
                {
                    return RequestScope.Resolve(serviceType);
                }
                catch (ResolutionFailedException)
                {
                    return null;
                }
            }

            public IEnumerable<object> GetServices(Type serviceType)
            {
                try
                {
                    return RequestScope.ResolveAll(serviceType);
                }
                catch (ResolutionFailedException)
                {
                    return new object[0];
                }
            }
        }
    }
}

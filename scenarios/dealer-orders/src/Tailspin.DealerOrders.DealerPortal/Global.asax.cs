using System;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using log4net;
using Tailspin.DealerOrders.Infrastructure.Logging;

namespace Tailspin.DealerOrders.DealerPortal
{
    public class MvcApplication : HttpApplication
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MvcApplication));

        protected void Application_Start()
        {
            LogSetup.Configure("DealerPortal");
            MvcHandler.DisableMvcResponseHeader = true;
            AntiForgeryConfig.UniqueClaimTypeIdentifier = System.Security.Claims.ClaimTypes.NameIdentifier;
            UnityConfig.Register();
            MappingConfig.Register();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        protected void Application_EndRequest()
        {
            UnityConfig.EndRequest(Context);
        }

        protected void Application_Error()
        {
            var ex = Server.GetLastError();
            if (ex is HttpException http && http.GetHttpCode() == 404) return;
            Log.Error($"Unhandled error on {Request.HttpMethod} {Request.RawUrl} ({User?.Identity?.Name})", ex);
        }

        protected void Application_End()
        {
            UnityConfig.Container?.Dispose();
        }
    }
}

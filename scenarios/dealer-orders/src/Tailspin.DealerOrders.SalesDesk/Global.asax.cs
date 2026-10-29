using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;
using log4net;
using Tailspin.DealerOrders.Infrastructure.Logging;

namespace Tailspin.DealerOrders.SalesDesk
{
    public class MvcApplication : HttpApplication
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MvcApplication));

        protected void Application_Start()
        {
            LogSetup.Configure("SalesDesk");
            MvcHandler.DisableMvcResponseHeader = true;
            AutofacConfig.Register(GlobalConfiguration.Configuration);
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }

        protected void Application_Error()
        {
            var ex = Server.GetLastError();
            if (ex is HttpException http && http.GetHttpCode() == 404) return;
            Log.Error($"Unhandled error on {Request.HttpMethod} {Request.RawUrl} ({User?.Identity?.Name})", ex);
        }
    }
}

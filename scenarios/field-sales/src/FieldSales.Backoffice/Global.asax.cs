using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using log4net;

namespace Wingtip.FieldSales.Backoffice
{
    public class MvcApplication : HttpApplication
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MvcApplication));

        protected void Application_Start()
        {
            log4net.Config.XmlConfigurator.Configure();
            MvcHandler.DisableMvcResponseHeader = true;
            AutofacConfig.Register();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            Log.InfoFormat("Field Sales Backoffice {0} starting", typeof(MvcApplication).Assembly.GetName().Version);
        }

        protected void Application_Error()
        {
            var ex = Server.GetLastError();
            if (ex != null && !(ex is HttpException && ((HttpException)ex).GetHttpCode() == 404)) Log.Error("Unhandled error", ex);
        }
    }
}

using System;
using System.IO;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using log4net;

namespace Lucerne.ProjectSites.Dashboards
{
    public class MvcApplication : HttpApplication
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MvcApplication));

        protected void Application_Start()
        {
            log4net.Config.XmlConfigurator.ConfigureAndWatch(new FileInfo(Server.MapPath("~/log4net.config")));
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            UnityConfig.Register();
            MvcHandler.DisableMvcResponseHeader = true;
            Log.Info("Project dashboards started");
        }

        protected void Application_Error()
        {
            var ex = Server.GetLastError();
            if (ex != null && !(ex is HttpException && ((HttpException)ex).GetHttpCode() == 404))
            {
                Log.Error(Request.HttpMethod + " " + Request.RawUrl, ex);
            }
        }
    }
}

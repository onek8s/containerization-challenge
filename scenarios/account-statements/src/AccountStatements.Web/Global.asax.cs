using System;
using System.Diagnostics;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Woodgrove.Statements.Web
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            MvcHandler.DisableMvcResponseHeader = true;
            Trace.TraceInformation("Statements portal started {0:u}", DateTime.Now);
        }

        protected void Application_Error()
        {
            var ex = Server.GetLastError();
            if (ex != null && !(ex is HttpException && ((HttpException)ex).GetHttpCode() == 404))
                Trace.TraceError("{0} {1}: {2}", Request.HttpMethod, Request.RawUrl, ex);
        }
    }
}

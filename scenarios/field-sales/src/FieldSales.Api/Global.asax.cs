using System;
using System.Web;
using log4net;

namespace Wingtip.FieldSales.Api
{
    public class WebApiApplication : HttpApplication
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(WebApiApplication));

        protected void Application_Start()
        {
            log4net.Config.XmlConfigurator.Configure();
            Log.InfoFormat("Field Sales API {0} starting", typeof(WebApiApplication).Assembly.GetName().Version);
        }

        protected void Application_Error()
        {
            var ex = Server.GetLastError();
            if (ex != null) Log.Error("Unhandled error", ex);
        }

        protected void Application_End()
        {
            Log.InfoFormat("Field Sales API stopping ({0})", System.Web.Hosting.HostingEnvironment.ShutdownReason);
            LogManager.Shutdown();
        }
    }
}

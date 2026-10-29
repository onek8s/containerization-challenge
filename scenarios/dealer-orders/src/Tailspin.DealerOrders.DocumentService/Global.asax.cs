using System;
using System.Web;
using log4net;
using Tailspin.DealerOrders.Infrastructure.Logging;

namespace Tailspin.DealerOrders.DocumentService
{
    public class Global : HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            LogSetup.Configure("DocumentService");
            ServiceRuntime.Initialize();
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            var ex = Server.GetLastError();
            if (ex != null) LogManager.GetLogger(typeof(Global)).Error("Unhandled error", ex);
        }

        protected void Application_End(object sender, EventArgs e)
        {
            ServiceRuntime.Shutdown();
        }
    }
}

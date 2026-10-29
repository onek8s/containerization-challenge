using System;
using System.Diagnostics;
using System.Web;
using System.Web.Http;

namespace Contoso.Purchasing.Api
{
    public class WebApiApplication : HttpApplication
    {
        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);
            Trace.TraceInformation("Purchasing API started on {0}", Environment.MachineName);
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            var error = Server.GetLastError();
            if (error != null)
            {
                Trace.TraceError("Unhandled error: {0}", error);
            }
        }
    }
}

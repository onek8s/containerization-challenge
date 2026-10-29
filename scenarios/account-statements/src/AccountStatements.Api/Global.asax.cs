using System;
using System.Diagnostics;
using System.Web;
using System.Web.Http;

namespace Woodgrove.Statements.Api
{
    public class WebApiApplication : HttpApplication
    {
        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);
            Trace.TraceInformation("Document API started {0:u}", DateTime.Now);
        }

        protected void Application_Error()
        {
            var ex = Server.GetLastError();
            if (ex != null) Trace.TraceError("Unhandled error: {0}", ex);
        }
    }
}

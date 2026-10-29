using System;
using System.Web;
using Northwind.Inspections.Infrastructure.Diagnostics;

namespace Northwind.Inspections.Service
{
    public class Global : HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            InspectionsLog.UseSource(InspectionsLog.ServiceSource);
            var settings = ServiceRuntime.Settings;
            InspectionsLog.Info(EventId.Started, "Inspections service started on {0} as {1}. Photo share {2}.",
                Environment.MachineName, System.Security.Principal.WindowsIdentity.GetCurrent().Name, settings.PhotoShare);
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            var ex = Server.GetLastError();
            if (ex is HttpException && ((HttpException)ex).GetHttpCode() == 404)
            {
                return;
            }
            InspectionsLog.Error(EventId.UnhandledError, ex, "Unhandled error for {0}.", Request.RawUrl);
        }
    }
}

using System;
using System.Diagnostics;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Tailspin.CustomerCare.Data.Services;
using Tailspin.CustomerCare.Web.Realtime;
using Tailspin.CustomerCare.Web.Security;

namespace Tailspin.CustomerCare.Web
{
    public class Global : HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            MvcHandler.DisableMvcResponseHeader = true;

            CaseService.CaseChanged += QueueNotifier.OnCaseChanged;
            Trace.TraceInformation("Customer Care portal started on {0}.", Environment.MachineName);
        }

        protected void Session_Start(object sender, EventArgs e)
        {
            Application.Lock();
            Application["ActiveSessions"] = (int)(Application["ActiveSessions"] ?? 0) + 1;
            Application.UnLock();

            // Resolve display name, e-mail and groups once per session; the directory lookup is slow.
            if (User != null && User.Identity.IsAuthenticated)
            {
                CareUser.LoadProfile(new HttpContextWrapper(Context));
            }
        }

        protected void Session_End(object sender, EventArgs e)
        {
            // Only raised for InProc session state.
            Application.Lock();
            Application["ActiveSessions"] = Math.Max(0, (int)(Application["ActiveSessions"] ?? 1) - 1);
            Application.UnLock();
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            var ex = Server.GetLastError();
            if (ex == null) return;

            var http = ex as HttpException;
            if (http != null && http.GetHttpCode() == 404) return;

            Trace.TraceError("Unhandled error on {0} for {1}: {2}", Request.RawUrl, User != null ? User.Identity.Name : "-", ex);
        }

        protected void Application_End()
        {
            Trace.TraceInformation("Customer Care portal stopping ({0}).", System.Web.Hosting.HostingEnvironment.ShutdownReason);
        }
    }
}

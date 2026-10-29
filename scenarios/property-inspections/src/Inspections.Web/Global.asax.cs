using System;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using Northwind.Inspections.Infrastructure.Diagnostics;

namespace Northwind.Inspections.Web
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            InspectionsLog.UseSource(InspectionsLog.PortalSource);
            MvcHandler.DisableMvcResponseHeader = true;
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            InspectionsLog.Info(EventId.Started, "Inspections portal started on {0}.", Environment.MachineName);
        }

        protected void Application_PostAuthenticateRequest(object sender, EventArgs e)
        {
            var identity = Context.User == null ? null : Context.User.Identity as FormsIdentity;
            if (identity == null || !identity.IsAuthenticated)
            {
                return;
            }
            var user = PortalUser.FromTicket(identity.Ticket);
            Context.User = new GenericPrincipal(identity, user.IsSupervisor ? new[] { PortalUser.AdjusterRole, PortalUser.SupervisorRole } : new[] { PortalUser.AdjusterRole });
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            var ex = Server.GetLastError();
            var http = ex as HttpException;
            if (http != null && http.GetHttpCode() < 500)
            {
                return;
            }
            InspectionsLog.Error(EventId.UnhandledError, ex, "Unhandled error for {0} ({1}).", Request.RawUrl, User == null ? "anonymous" : User.Identity.Name);
        }
    }
}

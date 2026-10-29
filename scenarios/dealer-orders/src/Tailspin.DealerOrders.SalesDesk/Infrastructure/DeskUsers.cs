using System;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using Tailspin.DealerOrders.Infrastructure;
using Tailspin.DealerOrders.Processing.Sales;

namespace Tailspin.DealerOrders.SalesDesk.Infrastructure
{
    public enum DeskRole
    {
        SalesDesk,
        SalesManager,
        DealerAdmin
    }

    /// <summary>
    /// Maps the sales desk roles to Active Directory groups (Windows authentication, WindowsTokenRoleProvider) and
    /// the Windows account to the AX user that commands run as.
    /// </summary>
    public class DeskUsers
    {
        public static string GroupFor(DeskRole role)
        {
            switch (role)
            {
                case DeskRole.SalesManager: return Settings.Get("Desk.Group.SalesManagers", @"CORP\TSM-SalesManagers");
                case DeskRole.DealerAdmin: return Settings.Get("Desk.Group.DealerAdmins", @"CORP\TSM-DealerAdmins");
                default: return Settings.Get("Desk.Group.SalesDesk", @"CORP\TSM-SalesDesk");
            }
        }

        public static bool IsIn(IPrincipal user, DeskRole role) => user.IsInRole(GroupFor(role));

        /// <summary>
        /// The AX user for a desk user: commands are run in AX with LogonAs for this user, so the AX audit trail and
        /// AX security apply to the desk user and not the connector's service account. CORP\mvirtanen -> mvirtanen.
        /// </summary>
        public static string AxUserFor(IPrincipal user)
        {
            var name = user.Identity.Name ?? "";
            var slash = name.IndexOf('\\');
            return slash >= 0 ? name.Substring(slash + 1) : name;
        }
    }

    public class DeskAuthorizeAttribute : AuthorizeAttribute
    {
        private readonly DeskRole[] _roles;

        public DeskAuthorizeAttribute(params DeskRole[] roles)
        {
            _roles = roles;
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            var user = httpContext.User;
            if (user == null || !user.Identity.IsAuthenticated) return false;
            return Array.Exists(_roles, r => DeskUsers.IsIn(user, r));
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            // Windows authentication already happened; a 401 would only make the browser ask for credentials again.
            filterContext.Result = filterContext.HttpContext.User.Identity.IsAuthenticated
                ? (ActionResult)new ViewResult { ViewName = "Forbidden" }
                : new HttpUnauthorizedResult();
        }
    }

    public class DeskApiAuthorizeAttribute : System.Web.Http.AuthorizeAttribute
    {
        protected override bool IsAuthorized(System.Web.Http.Controllers.HttpActionContext actionContext)
        {
            var user = actionContext.RequestContext.Principal;
            return user != null && user.Identity.IsAuthenticated &&
                   (DeskUsers.IsIn(user, DeskRole.SalesDesk) || DeskUsers.IsIn(user, DeskRole.SalesManager) ||
                    user.IsInRole(Settings.Get("Desk.Group.Monitoring", @"CORP\TSM-AppMonitoring")));
        }
    }

    public class DeskRuleFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            if (context.ExceptionHandled || !(context.Exception is SalesRuleException)) return;
            context.Controller.TempData["Error"] = context.Exception.Message;
            var referrer = context.HttpContext.Request.UrlReferrer;
            context.Result = new RedirectResult(referrer != null ? referrer.PathAndQuery : "~/");
            context.ExceptionHandled = true;
        }
    }
}

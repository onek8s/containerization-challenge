using System;
using System.Configuration;
using System.Linq;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;

namespace Woodgrove.Statements.Web
{
    public enum PortalRoles
    {
        User,
        CreditControl,
        Admin
    }

    /// <summary>
    /// Portal roles are Active Directory groups (Windows authentication in IIS), configured in appSettings:
    /// Roles.User, Roles.CreditControl, Roles.Admin. Admins have every role, credit control includes user.
    /// </summary>
    public static class RoleMap
    {
        public static string[] Groups(PortalRoles role)
        {
            var value = ConfigurationManager.AppSettings["Roles." + role] ?? "";
            return value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(g => g.Trim()).ToArray();
        }

        public static bool IsIn(IPrincipal user, PortalRoles role)
        {
            if (user == null || !user.Identity.IsAuthenticated) return false;
            foreach (var r in Enum.GetValues(typeof(PortalRoles)).Cast<PortalRoles>().Where(r => r >= role))
                if (Groups(r).Any(user.IsInRole)) return true;
            return false;
        }

        /// <summary>Account name without the domain, for owner checks and audit columns.</summary>
        public static string AccountName(IPrincipal user)
        {
            return user?.Identity?.Name ?? "unknown";
        }
    }

    public class PortalRoleAttribute : AuthorizeAttribute
    {
        readonly PortalRoles _role;

        public PortalRoleAttribute(PortalRoles role)
        {
            _role = role;
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            return RoleMap.IsIn(httpContext.User, _role);
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (filterContext.HttpContext.User.Identity.IsAuthenticated)
                filterContext.Result = new HttpStatusCodeResult(403, "You do not have access to this page.");
            else
                base.HandleUnauthorizedRequest(filterContext);
        }
    }

    public static class PrincipalExtensions
    {
        public static bool IsCreditControl(this IPrincipal user)
        {
            return RoleMap.IsIn(user, PortalRoles.CreditControl);
        }

        public static bool IsAdmin(this IPrincipal user)
        {
            return RoleMap.IsIn(user, PortalRoles.Admin);
        }
    }
}

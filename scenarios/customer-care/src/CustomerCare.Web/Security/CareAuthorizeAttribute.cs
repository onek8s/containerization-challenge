using System.Web;
using System.Web.Mvc;

namespace Tailspin.CustomerCare.Web.Security
{
    /// <summary>MVC authorization against the configured Active Directory groups.</summary>
    public class CareAuthorizeAttribute : AuthorizeAttribute
    {
        public CareAuthorizeAttribute(CareRole role)
        {
            Role = role;
        }

        public CareRole Role { get; }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            if (httpContext.User == null || !httpContext.User.Identity.IsAuthenticated) return false;
            return CareUser.IsIn(httpContext, Role);
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (filterContext.HttpContext.User != null && filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                // Authenticated but not in the group: a 401 would make the browser prompt for other credentials.
                filterContext.Result = new HttpStatusCodeResult(403, "You do not have access to this page.");
                return;
            }

            base.HandleUnauthorizedRequest(filterContext);
        }
    }
}

using System;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;

namespace Lucerne.ProjectSites.Dashboards.Infrastructure
{
    /// <summary>
    /// Keeps SPHostUrl (sent by SharePoint when the add-in is launched or rendered as an app part) in the session,
    /// and makes sure the user is a Windows user, as high-trust user tokens carry the Windows SID.
    /// </summary>
    public sealed class SharePointContextFilterAttribute : ActionFilterAttribute
    {
        public const string HostUrlKey = "SPHostUrl";

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var http = filterContext.HttpContext;
            var hostUrl = http.Request.QueryString[HostUrlKey];
            if (!string.IsNullOrEmpty(hostUrl))
            {
                Uri parsed;
                if (!Uri.TryCreate(hostUrl, UriKind.Absolute, out parsed) || (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp))
                {
                    filterContext.Result = new HttpStatusCodeResult(400, "Invalid SPHostUrl");
                    return;
                }

                http.Session[HostUrlKey] = parsed.AbsoluteUri.TrimEnd('/');
            }

            if (!(http.User.Identity is WindowsIdentity))
            {
                filterContext.Result = new HttpStatusCodeResult(401, "Windows authentication is required");
            }
        }

        public static string HostUrl(HttpContextBase context)
        {
            return context.Session[HostUrlKey] as string;
        }

        public static string UserSid(HttpContextBase context)
        {
            var identity = context.User.Identity as WindowsIdentity;
            return identity == null || identity.User == null ? null : identity.User.Value;
        }
    }
}

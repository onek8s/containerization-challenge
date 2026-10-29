using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using Wingtip.FieldSales.Core;

namespace Wingtip.FieldSales.Api.Infrastructure
{
    /// <summary>
    /// Rejects requests from desktop clients older than FieldSales:MinimumClientVersion. The client sends its
    /// ClickOnce version in the X-FieldSales-Client header and offers an update when it gets 426.
    /// </summary>
    public class ClientVersionAttribute : ActionFilterAttribute
    {
        public const string HeaderName = "X-FieldSales-Client";

        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            var settings = (ApiSettings)actionContext.Request.GetDependencyScope().GetService(typeof(ApiSettings));
            var version = actionContext.Request.Headers.Contains(HeaderName)
                ? actionContext.Request.Headers.GetValues(HeaderName).FirstOrDefault()
                : null;

            if (!ClientVersionPolicy.IsSupported(version, settings.MinimumClientVersion))
            {
                actionContext.Response = actionContext.Request.CreateErrorResponse((HttpStatusCode)426,
                    "This version of Field Sales is no longer supported. Restart the application to install version " +
                    settings.MinimumClientVersion + " or later.");
            }
        }
    }
}

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Principal;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace Coho.Billing.Engine.Control
{
    /// <summary>Members of the operators group (OperatorsGroup setting); optionally also the accounts in ReprintCallers.</summary>
    public class OperatorsAttribute : AuthorizationFilterAttribute
    {
        public bool AllowReprintCallers { get; set; }

        public override void OnAuthorization(HttpActionContext actionContext)
        {
            var principal = actionContext.RequestContext.Principal as WindowsPrincipal;
            if (principal == null || !principal.Identity.IsAuthenticated)
            {
                actionContext.Response = actionContext.Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "Windows authentication required");
                return;
            }

            var settings = ControlHost.Engine.Settings;
            if (principal.IsInRole(settings.OperatorsGroup))
            {
                return;
            }

            if (AllowReprintCallers && settings.ReprintCallers.Any(c => string.Equals(c.Trim(), principal.Identity.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            EngineLog.Event(System.Diagnostics.EventLogEntryType.FailureAudit, 4001,
                "Control request " + actionContext.Request.Method + " " + actionContext.Request.RequestUri.AbsolutePath + " denied for " + principal.Identity.Name);
            actionContext.Response = actionContext.Request.CreateErrorResponse(HttpStatusCode.Forbidden, "Not a member of " + settings.OperatorsGroup);
        }
    }

    public class ControlExceptionFilter : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext context)
        {
            var exception = context.Exception;
            if (exception is RunStateException)
            {
                context.Response = context.Request.CreateErrorResponse(HttpStatusCode.Conflict, exception.Message);
            }
            else if (exception is RunBusyException)
            {
                context.Response = context.Request.CreateErrorResponse(HttpStatusCode.ServiceUnavailable, exception.Message);
                context.Response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
            }
            else
            {
                EngineLog.Error(exception, "Control request {0} {1} failed", context.Request.Method, context.Request.RequestUri.AbsolutePath);
            }
        }
    }
}

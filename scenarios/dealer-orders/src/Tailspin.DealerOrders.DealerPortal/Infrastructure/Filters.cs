using System;
using System.Web.Mvc;
using log4net;
using Tailspin.DealerOrders.Infrastructure;
using Tailspin.DealerOrders.Processing.Sales;

namespace Tailspin.DealerOrders.DealerPortal.Infrastructure
{
    /// <summary>Business rule violations become a message on the page the user came from (or a 400 for JSON calls).</summary>
    public class SalesRuleFilter : IExceptionFilter
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SalesRuleFilter));

        public void OnException(ExceptionContext context)
        {
            if (context.ExceptionHandled || !(context.Exception is SalesRuleException)) return;
            var message = context.Exception.Message;
            Log.InfoFormat("Rule: {0} ({1} {2})", message, context.HttpContext.User.Identity.Name, context.HttpContext.Request.RawUrl);

            if (context.HttpContext.Request.IsAjaxRequest())
            {
                context.HttpContext.Response.StatusCode = 400;
                context.Result = new ContentResult { Content = Newtonsoft.Json.JsonConvert.SerializeObject(new { error = message }), ContentType = "application/json" };
            }
            else
            {
                context.Controller.TempData["Error"] = message;
                var referrer = context.HttpContext.Request.UrlReferrer;
                context.Result = new RedirectResult(referrer != null && referrer.Host == context.HttpContext.Request.Url?.Host ? referrer.PathAndQuery : "~/");
            }
            context.ExceptionHandled = true;
        }
    }

    /// <summary>HTTPS everywhere except on test systems where Portal.RequireSsl is false.</summary>
    public class RequireHttpsWhenConfiguredAttribute : RequireHttpsAttribute
    {
        private static readonly bool Required = Settings.Bool("Portal.RequireSsl", true);

        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            if (Required) base.OnAuthorization(filterContext);
        }
    }

    /// <summary>Allows the action only for the given portal roles.</summary>
    public class PortalRoleAttribute : AuthorizeAttribute
    {
        public PortalRoleAttribute(params string[] roles)
        {
            Roles = string.Join(",", roles);
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (filterContext.HttpContext.User.Identity.IsAuthenticated)
                filterContext.Result = new ViewResult { ViewName = "Forbidden" };
            else
                base.HandleUnauthorizedRequest(filterContext);
        }
    }
}

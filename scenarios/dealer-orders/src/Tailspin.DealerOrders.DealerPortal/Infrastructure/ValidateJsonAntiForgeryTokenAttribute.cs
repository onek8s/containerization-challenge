using System;
using System.Web.Helpers;
using System.Web.Mvc;

namespace Tailspin.DealerOrders.DealerPortal.Infrastructure
{
    /// <summary>Anti-forgery check for JSON posts: the token comes in the RequestVerificationToken header (see common.js).</summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class ValidateJsonAntiForgeryTokenAttribute : FilterAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            var request = filterContext.HttpContext.Request;
            var cookie = request.Cookies[AntiForgeryConfig.CookieName];
            AntiForgery.Validate(cookie?.Value, request.Headers["RequestVerificationToken"]);
        }
    }
}

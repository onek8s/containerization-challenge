using System.Web.Mvc;
using Contoso.Purchasing.Core;
using Contoso.Purchasing.Web.Services;

namespace Contoso.Purchasing.Web.Controllers
{
    public abstract class PortalController : Controller
    {
        private PurchasingApiClient _api;

        protected PurchasingApiClient Api
        {
            get { return _api ?? (_api = new PurchasingApiClient(HttpContext)); }
        }

        /// <summary>
        /// The user profile is cached in session so every page does not need an extra API round trip.
        /// </summary>
        protected UserContext CurrentUser
        {
            get
            {
                var user = Session["Purchasing.User"] as UserContext;
                if (user == null)
                {
                    user = Api.Me();
                    Session["Purchasing.User"] = user;
                }
                return user;
            }
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);
            ViewBag.CurrentUser = CurrentUser;
        }

        protected void Flash(string message)
        {
            TempData["Flash"] = message;
        }
    }
}

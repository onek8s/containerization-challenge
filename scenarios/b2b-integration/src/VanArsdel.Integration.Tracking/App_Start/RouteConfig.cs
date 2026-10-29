using System.Web.Mvc;
using System.Web.Routing;

namespace VanArsdel.Integration.Tracking
{
    public static class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.LowercaseUrls = true;

            routes.MapRoute("Order", "orders/{partnerId}/{orderNumber}", new { controller = "Orders", action = "Details" });
            routes.MapRoute("Default", "{controller}/{action}/{id}", new { controller = "Orders", action = "Index", id = UrlParameter.Optional });
        }
    }
}

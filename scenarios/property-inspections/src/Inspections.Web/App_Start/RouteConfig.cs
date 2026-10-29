using System.Web.Mvc;
using System.Web.Routing;

namespace Northwind.Inspections.Web
{
    public static class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.IgnoreRoute("media/{*pathInfo}");
            routes.LowercaseUrls = true;

            routes.MapRoute("Inspection", "inspections/{id}", new { controller = "Inspections", action = "Details" }, new { id = @"\d+" });
            routes.MapRoute("Default", "{controller}/{action}/{id}", new { controller = "Inspections", action = "Index", id = UrlParameter.Optional });
        }
    }
}

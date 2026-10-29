using System.Web.Mvc;
using System.Web.Routing;

namespace Tailspin.CustomerCare.Web
{
    public static class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.IgnoreRoute("{resource}.ashx/{*pathInfo}");
            routes.IgnoreRoute("signalr/{*pathInfo}");
            routes.IgnoreRoute("admin/jobs/{*pathInfo}");

            // Friendly URLs for the Web Forms pages.
            routes.MapPageRoute("case", "cases/{id}", "~/Cases/Case.aspx", false, null, new RouteValueDictionary { { "id", @"\d+" } });
            routes.MapPageRoute("case-new", "cases/new", "~/Cases/New.aspx");
            routes.MapPageRoute("cases", "cases", "~/Cases/Default.aspx");
            routes.MapPageRoute("customer", "customers/{id}", "~/Customers/Customer.aspx", false, null, new RouteValueDictionary { { "id", @"\d+" } });
            routes.MapPageRoute("customers", "customers", "~/Customers/Default.aspx");

            routes.MapRoute("kb-article", "kb/{slug}", new { controller = "Knowledge", action = "Article" }, new { slug = @"[a-z0-9\-]+" });
            routes.MapRoute("kb", "kb", new { controller = "Knowledge", action = "Index" });
            routes.MapRoute("board", "board/{queue}", new { controller = "Board", action = "Index", queue = UrlParameter.Optional });
            routes.MapRoute("Default", "{controller}/{action}/{id}", new { action = "Index", id = UrlParameter.Optional },
                new { controller = "Knowledge|Board|Search" });
        }
    }
}

using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;
using Tailspin.DealerOrders.SalesDesk.Infrastructure;

namespace Tailspin.DealerOrders.SalesDesk
{
    public static class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.LowercaseUrls = true;
            routes.MapRoute("Default", "{controller}/{action}/{id}", new { controller = "Home", action = "Index", id = UrlParameter.Optional });
        }
    }

    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            config.MapHttpAttributeRoutes();
            config.Formatters.Remove(config.Formatters.XmlFormatter);
            config.Formatters.JsonFormatter.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
            config.Filters.Add(new DeskApiAuthorizeAttribute());
        }
    }

    public static class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new DeskRuleFilter());
            filters.Add(new HandleErrorAttribute());
            filters.Add(new DeskAuthorizeAttribute(DeskRole.SalesDesk, DeskRole.SalesManager, DeskRole.DealerAdmin));
        }
    }
}

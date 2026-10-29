using System.Web.Mvc;
using Tailspin.DealerOrders.DealerPortal.Infrastructure;

namespace Tailspin.DealerOrders.DealerPortal
{
    public static class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new SalesRuleFilter());
            filters.Add(new HandleErrorAttribute());
            filters.Add(new AuthorizeAttribute());
            filters.Add(new RequireHttpsWhenConfiguredAttribute());
        }
    }
}

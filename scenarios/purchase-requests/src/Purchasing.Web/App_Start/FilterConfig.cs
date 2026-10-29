using System.Web.Mvc;
using Contoso.Purchasing.Web.Services;

namespace Contoso.Purchasing.Web
{
    public static class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new AuthorizeAttribute());
            filters.Add(new ApiErrorFilter());
            filters.Add(new HandleErrorAttribute());
        }
    }
}

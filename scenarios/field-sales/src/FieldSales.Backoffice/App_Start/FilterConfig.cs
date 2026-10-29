using System.Web.Mvc;
using Wingtip.FieldSales.Data;

namespace Wingtip.FieldSales.Backoffice
{
    public static class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            filters.Add(new AuthorizeAttribute { Roles = Roles.SalesManager + "," + Roles.Administrator });
        }
    }
}

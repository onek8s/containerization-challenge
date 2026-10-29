using System.Web.Mvc;

namespace Woodgrove.Statements.Web
{
    public static class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            filters.Add(new PortalRoleAttribute(PortalRoles.User));
        }
    }
}

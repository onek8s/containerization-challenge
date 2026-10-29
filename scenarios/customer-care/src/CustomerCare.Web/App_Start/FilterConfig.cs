using System.Web.Mvc;
using Tailspin.CustomerCare.Web.Security;

namespace Tailspin.CustomerCare.Web
{
    public static class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            filters.Add(new CareAuthorizeAttribute(CareRole.Agent));
        }
    }
}

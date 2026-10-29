using System.Web;
using Hangfire.Dashboard;

namespace Tailspin.CustomerCare.Web.Security
{
    /// <summary>Only supervisors may open the background job dashboard at /admin/jobs.</summary>
    public class SupervisorDashboardFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var http = HttpContext.Current;
            if (http == null || http.User == null || !http.User.Identity.IsAuthenticated) return false;
            return CareUser.IsIn(new HttpContextWrapper(http), CareRole.Supervisor);
        }
    }
}

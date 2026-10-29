using Fabrikam.Housing.Hosting.Security;
using Hangfire.Dashboard;

namespace Fabrikam.Housing.Jobs.Infrastructure
{
    /// <summary>The Hangfire dashboard is for the systems administrators and finance.</summary>
    public class DashboardAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var user = context.GetHttpContext().User;
            return user.Identity != null && user.Identity.IsAuthenticated
                && (user.IsInRole(HousingRoles.Admin) || user.IsInRole(HousingRoles.Finance));
        }
    }
}

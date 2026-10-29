using System.Linq;
using System.Security.Claims;
using Fabrikam.Housing.Hosting.Security;

namespace Fabrikam.Housing.BackOffice.Infrastructure
{
    public static class UserExtensions
    {
        /// <summary>Staff login as CORP\user, used in audit and work order history.</summary>
        public static string Login(this ClaimsPrincipal user)
        {
            return user.FindFirst("preferred_username")?.Value
                ?? user.FindFirst("sub")?.Value
                ?? user.Identity?.Name
                ?? "unknown";
        }

        public static string DisplayName(this ClaimsPrincipal user)
        {
            return user.FindFirst("name")?.Value ?? user.Identity?.Name ?? user.Login();
        }

        public static string Patch(this ClaimsPrincipal user)
        {
            return user.FindFirst(HousingClaimTypes.Patch)?.Value;
        }

        public static bool HasAnyRole(this ClaimsPrincipal user, params string[] roles)
        {
            return roles.Any(user.IsInRole);
        }
    }
}

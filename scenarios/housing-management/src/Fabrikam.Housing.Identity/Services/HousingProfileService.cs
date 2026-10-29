using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Fabrikam.Housing.Hosting.Security;
using Fabrikam.Housing.Identity.Data;
using IdentityModel;
using IdentityServer4.Extensions;
using IdentityServer4.Models;
using IdentityServer4.Services;
using Microsoft.AspNetCore.Identity;

namespace Fabrikam.Housing.Identity.Services
{
    /// <summary>Issues name, e-mail, roles and the housing claims (tenancy, tenant id, patch).</summary>
    public class HousingProfileService : IProfileService
    {
        private readonly UserManager<ApplicationUser> _users;

        public HousingProfileService(UserManager<ApplicationUser> users)
        {
            _users = users;
        }

        public async Task GetProfileDataAsync(ProfileDataRequestContext context)
        {
            var user = await _users.FindByIdAsync(context.Subject.GetSubjectId());
            if (user == null) return;

            var claims = new List<Claim>
            {
                new Claim(JwtClaimTypes.Name, user.FullName ?? user.UserName),
                new Claim(JwtClaimTypes.PreferredUserName, user.UserName)
            };
            if (!string.IsNullOrEmpty(user.Email)) claims.Add(new Claim(JwtClaimTypes.Email, user.Email));
            if (user.TenantId.HasValue) claims.Add(new Claim(HousingClaimTypes.TenantId, user.TenantId.Value.ToString()));
            if (!string.IsNullOrEmpty(user.TenancyReference)) claims.Add(new Claim(HousingClaimTypes.TenancyReference, user.TenancyReference));
            if (!string.IsNullOrEmpty(user.Patch)) claims.Add(new Claim(HousingClaimTypes.Patch, user.Patch));
            claims.AddRange((await _users.GetClaimsAsync(user)).Where(c => c.Type == HousingClaimTypes.Role));

            context.AddRequestedClaims(claims);
        }

        public async Task IsActiveAsync(IsActiveContext context)
        {
            var user = await _users.FindByIdAsync(context.Subject.GetSubjectId());
            context.IsActive = user != null && (!user.LockoutEnabled || user.LockoutEnd == null || user.LockoutEnd < System.DateTimeOffset.UtcNow);
        }
    }
}

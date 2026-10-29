using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudyData.Data;

namespace StudyData.Edc.Web.Services;

/// <summary>Adds the user's sites, signing right and display name to the sign-in cookie.</summary>
public class SiteClaimsFactory : UserClaimsPrincipalFactory<SiteUser, IdentityRole>
{
    public const string SiteClaim = "studydata:site";
    public const string SignSiteClaim = "studydata:sign";

    private readonly StudyDataContext _db;

    public SiteClaimsFactory(UserManager<SiteUser> users, RoleManager<IdentityRole> roles, IOptions<IdentityOptions> options, StudyDataContext db)
        : base(users, roles, options)
    {
        _db = db;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(SiteUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim("name", user.FullName));
        identity.AddClaim(new Claim("site_role", user.SiteRole));
        var sites = await _db.SiteAssignments.Where(a => a.UserId == user.Id).ToListAsync();
        foreach (var a in sites)
        {
            identity.AddClaim(new Claim(SiteClaim, a.SiteId.ToString()));
            if (a.CanSign)
            {
                identity.AddClaim(new Claim(SignSiteClaim, a.SiteId.ToString()));
            }
        }

        return identity;
    }
}

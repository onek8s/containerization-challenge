using System.Security.Claims;
using IdentityModel;
using IdentityServer4.AspNetIdentity;
using IdentityServer4.Models;
using Microsoft.AspNetCore.Identity;
using Proseware.ControlTower.Identity.Data;

namespace Proseware.ControlTower.Identity.Services;

/// <summary>Adds roles, display name and the customer id to tokens; disabled users are inactive.</summary>
public sealed class ControlTowerProfileService : ProfileService<ApplicationUser>
{
    public const string CustomerIdClaim = "customer_id";

    public ControlTowerProfileService(UserManager<ApplicationUser> userManager, IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory)
        : base(userManager, claimsFactory)
    {
    }

    protected override async Task GetProfileDataAsync(ProfileDataRequestContext context, ApplicationUser user)
    {
        await base.GetProfileDataAsync(context, user);

        var claims = new List<Claim>
        {
            new(JwtClaimTypes.Name, string.IsNullOrEmpty(user.DisplayName) ? user.UserName : user.DisplayName),
            new("preferred_username", user.UserName)
        };
        foreach (var role in await UserManager.GetRolesAsync(user))
            claims.Add(new Claim(JwtClaimTypes.Role, role));
        if (user.CustomerId is { } customerId)
            claims.Add(new Claim(CustomerIdClaim, customerId.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        context.AddRequestedClaims(claims);
        // Access tokens for the APIs always carry the role and customer, independent of requested claims.
        if (context.Caller == IdentityServer4.IdentityServerConstants.ProfileDataCallers.ClaimsProviderAccessToken)
            context.IssuedClaims.AddRange(claims.Where(c => c.Type is JwtClaimTypes.Role or CustomerIdClaim && !context.IssuedClaims.Any(i => i.Type == c.Type && i.Value == c.Value)));
    }

    protected override Task IsActiveAsync(IsActiveContext context, ApplicationUser user)
    {
        context.IsActive = !user.Disabled && (!user.LockoutEnabled || user.LockoutEnd is null || user.LockoutEnd < DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }
}

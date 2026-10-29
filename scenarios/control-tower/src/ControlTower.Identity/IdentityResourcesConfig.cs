using IdentityServer4.Models;

namespace Proseware.ControlTower.Identity;

public static class IdentityResourcesConfig
{
    public static IEnumerable<IdentityResource> All { get; } = new IdentityResource[]
    {
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        new IdentityResources.Email(),
        new("roles", "Roles", new[] { "role" }),
        new("customer", "Customer account", new[] { Services.ControlTowerProfileService.CustomerIdClaim })
    };
}

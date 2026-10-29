using Microsoft.AspNetCore.Authorization;
using VanArsdel.BuildingBlocks.Security;
using VanArsdel.Contracts;

namespace VanArsdel.Reporting.Api;

public static class Policies
{
    public const string Read = "reporting.read";
    public const string Run = "reporting.run";

    public static void Configure(AuthorizationOptions options)
    {
        options.AddScopePolicy(Read, Scopes.Reporting, Roles.Finance, Roles.Merchandiser, Roles.StoreManager);
        options.AddScopePolicy(Run, Scopes.Reporting + ".run", Roles.Finance);
    }
}

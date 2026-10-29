using Microsoft.AspNetCore.Authorization;
using VanArsdel.BuildingBlocks.Security;
using VanArsdel.Contracts;

namespace VanArsdel.Catalog.Api
{
    public static class Policies
    {
        public const string Read = "catalog.read";
        public const string Write = "catalog.write";

        public static void Configure(AuthorizationOptions options)
        {
            options.AddScopePolicy(Read, Scopes.Catalog, Roles.Merchandiser, Roles.CustomerService, Roles.StoreManager, Roles.StoreStaff, Roles.Finance);
            options.AddScopePolicy(Write, Scopes.Catalog + ".write", Roles.Merchandiser);
        }
    }
}

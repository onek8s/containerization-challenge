using Microsoft.AspNetCore.Authorization;
using VanArsdel.BuildingBlocks.Security;
using VanArsdel.Contracts;

namespace VanArsdel.StoreOps.Api
{
    public static class Policies
    {
        public const string Read = "storeops.read";
        public const string Store = "storeops.store";
        public const string Manage = "storeops.manage";

        public static void Configure(AuthorizationOptions options)
        {
            options.AddScopePolicy(Read, Scopes.StoreOps, Roles.StoreManager, Roles.StoreStaff, Roles.Merchandiser, Roles.CustomerService, Roles.Finance);
            options.AddScopePolicy(Store, Scopes.StoreOps + ".store", Roles.StoreManager, Roles.StoreStaff);
            options.AddScopePolicy(Manage, Scopes.StoreOps + ".manage", Roles.Merchandiser);
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using VanArsdel.BuildingBlocks.Security;
using VanArsdel.Contracts;

namespace VanArsdel.Pricing.Api
{
    public static class Policies
    {
        public const string Read = "pricing.read";
        public const string Write = "pricing.write";

        public static void Configure(AuthorizationOptions options)
        {
            options.AddScopePolicy(Read, Scopes.Pricing, Roles.Merchandiser, Roles.CustomerService, Roles.StoreManager, Roles.StoreStaff, Roles.Finance);
            options.AddScopePolicy(Write, Scopes.Pricing + ".write", Roles.Merchandiser);
        }
    }
}

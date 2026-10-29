using Microsoft.AspNetCore.Authorization;
using VanArsdel.BuildingBlocks.Security;
using VanArsdel.Contracts;

namespace VanArsdel.Inventory.Api
{
    public static class Policies
    {
        public const string Read = "inventory.read";
        public const string Adjust = "inventory.adjust";
        public const string Allocate = "inventory.allocate";

        public static void Configure(AuthorizationOptions options)
        {
            options.AddScopePolicy(Read, Scopes.Inventory, Roles.Merchandiser, Roles.CustomerService, Roles.StoreManager, Roles.StoreStaff, Roles.Finance);
            options.AddScopePolicy(Adjust, Scopes.Inventory + ".write", Roles.StoreManager, Roles.Merchandiser);
            options.AddScopePolicy(Allocate, Scopes.Inventory + ".allocate");
        }
    }
}

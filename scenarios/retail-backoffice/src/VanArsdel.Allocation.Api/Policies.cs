using Microsoft.AspNetCore.Authorization;
using VanArsdel.BuildingBlocks.Security;
using VanArsdel.Contracts;

namespace VanArsdel.Allocation.Api
{
    public static class Policies
    {
        public const string Read = "allocation.read";
        public const string Admin = "allocation.admin";

        public static void Configure(AuthorizationOptions options)
        {
            options.AddScopePolicy(Read, Scopes.Orders, Roles.CustomerService, Roles.Merchandiser);
            options.AddScopePolicy(Admin, Scopes.Orders + ".admin");
        }
    }
}

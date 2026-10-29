using Microsoft.AspNetCore.Authorization;
using VanArsdel.BuildingBlocks.Security;
using VanArsdel.Contracts;

namespace VanArsdel.Customers.Api
{
    public static class Policies
    {
        public const string Read = "customers.read";
        public const string Write = "customers.write";
        public const string Erase = "customers.erase";

        public static void Configure(AuthorizationOptions options)
        {
            options.AddScopePolicy(Read, Scopes.Customers, Roles.CustomerService, Roles.StoreManager, Roles.StoreStaff);
            options.AddScopePolicy(Write, Scopes.Customers + ".write", Roles.CustomerService, Roles.StoreManager, Roles.StoreStaff);
            options.AddScopePolicy(Erase, Scopes.Customers + ".erase", Roles.CustomerService);
        }
    }
}

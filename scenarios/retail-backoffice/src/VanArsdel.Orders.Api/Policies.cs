using Microsoft.AspNetCore.Authorization;
using VanArsdel.BuildingBlocks.Security;
using VanArsdel.Contracts;

namespace VanArsdel.Orders.Api;

public static class Policies
{
    public const string Read = "orders.read";
    public const string Manage = "orders.manage";
    public const string StoreOrder = "orders.store";

    public static void Configure(AuthorizationOptions options)
    {
        options.AddScopePolicy(Read, Scopes.Orders, Roles.CustomerService, Roles.StoreManager, Roles.StoreStaff, Roles.Finance, Roles.Merchandiser);
        options.AddScopePolicy(Manage, Scopes.Orders + ".write", Roles.CustomerService);
        options.AddScopePolicy(StoreOrder, Scopes.Orders + ".store", Roles.StoreManager, Roles.StoreStaff);
    }
}

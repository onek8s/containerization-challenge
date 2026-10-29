using Microsoft.AspNetCore.Authorization;
using VanArsdel.BuildingBlocks.Security;
using VanArsdel.Contracts;

namespace VanArsdel.Returns.Api;

public static class Policies
{
    public const string Read = "returns.read";
    public const string Register = "returns.register";
    public const string Handle = "returns.handle";

    public static void Configure(AuthorizationOptions options)
    {
        options.AddScopePolicy(Read, Scopes.Returns, Roles.CustomerService, Roles.StoreManager, Roles.StoreStaff, Roles.Finance);
        options.AddScopePolicy(Register, Scopes.Returns + ".register", Roles.CustomerService);
        options.AddScopePolicy(Handle, Scopes.Returns + ".write", Roles.CustomerService, Roles.StoreManager, Roles.StoreStaff);
    }
}

using VanArsdel.Contracts;

namespace VanArsdel.LabStubs.Oidc;

public record LabUser(string UserName, string Name, string Email, string[] Roles, string StoreCode = null);

/// <summary>Users of the lab identity provider. Password is "lab" for everyone.</summary>
public static class LabUsers
{
    public const string Password = "lab";
    public const string ClientSecret = "dev-secret";

    public static readonly LabUser[] All =
    {
        new("admin", "Lab Admin", "admin@vanarsdel.example", new[] { Roles.Admin }),
        new("merch", "Mia Merchandiser", "mia@vanarsdel.example", new[] { Roles.Merchandiser }),
        new("cs", "Carl Service", "carl@vanarsdel.example", new[] { Roles.CustomerService }),
        new("finance", "Fiona Finance", "fiona@vanarsdel.example", new[] { Roles.Finance }),
        new("fi012.manager", "Sanna Manager", "fi012@vanarsdel.example", new[] { Roles.StoreManager }, "FI012"),
        new("fi012.staff", "Ville Staff", "fi012.staff@vanarsdel.example", new[] { Roles.StoreStaff }, "FI012"),
        new("se003.manager", "Sara Chef", "se003@vanarsdel.example", new[] { Roles.StoreManager }, "SE003"),
        new("dc01.staff", "Dan Warehouse", "dc01@vanarsdel.example", new[] { Roles.StoreStaff }, "DC01"),
    };

    public static LabUser Find(string userName) =>
        All.FirstOrDefault(u => string.Equals(u.UserName, userName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Public clients (the React app) have no secret and must use PKCE.</summary>
    public static bool IsPublicClient(string clientId) => clientId == "storemanager";
}

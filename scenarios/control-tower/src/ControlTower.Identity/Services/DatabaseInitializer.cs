using IdentityServer4.EntityFramework.DbContexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Proseware.ControlTower.Identity.Data;

namespace Proseware.ControlTower.Identity.Services;

/// <summary>Creates the identity database and seeds roles and users. Used by --initialize and in Development.</summary>
public sealed class DatabaseInitializer
{
    private readonly AuthDbContext _auth;
    private readonly PersistedGrantDbContext _grants;
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(AuthDbContext auth, PersistedGrantDbContext grants, UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles, ILogger<DatabaseInitializer> logger)
    {
        _auth = auth;
        _grants = grants;
        _users = users;
        _roles = roles;
        _logger = logger;
    }

    public async Task CreateSchemaAsync()
    {
        if (await _auth.Database.EnsureCreatedAsync())
            _logger.LogInformation("Identity database created");

        // Both contexts live in one database; EnsureCreated only creates the tables of the first one.
        if (!await GrantTablesExistAsync())
        {
            var creator = (RelationalDatabaseCreator)_grants.Database.GetService<IDatabaseCreator>();
            await creator.CreateTablesAsync();
            _logger.LogInformation("Operational store tables created");
        }
    }

    private async Task<bool> GrantTablesExistAsync()
    {
        var connection = _grants.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = _grants.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"
            ? "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'PersistedGrants'"
            : "SELECT CASE WHEN OBJECT_ID(N'auth.PersistedGrants', N'U') IS NULL THEN 0 ELSE 1 END";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task SeedRolesAsync()
    {
        foreach (var role in Roles.All)
        {
            if (!await _roles.RoleExistsAsync(role))
                await _roles.CreateAsync(new IdentityRole(role));
        }
    }

    public async Task<IdentityResult> CreateUserAsync(string userName, string email, string password, string displayName, IEnumerable<string> roles, int? customerId = null)
    {
        var existing = await _users.FindByNameAsync(userName);
        if (existing is not null) return IdentityResult.Success;
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            CustomerId = customerId,
            IsStaff = customerId is null,
            CreatedUtc = DateTime.UtcNow
        };
        var result = await _users.CreateAsync(user, password);
        if (!result.Succeeded) return result;
        result = await _users.AddToRolesAsync(user, roles);
        _logger.LogInformation("User {User} created", userName);
        return result;
    }

    public async Task SeedDevelopmentUsersAsync()
    {
        const string pw = "Proseware#2024";
        await CreateUserAsync("admin", "admin@proseware-freight.example", pw, "Control Tower Admin", new[] { Roles.Admin, Roles.Operations, Roles.Booking });
        await CreateUserAsync("ops.anna", "anna.lehtonen@proseware-freight.example", pw, "Anna Lehtonen", new[] { Roles.Operations });
        await CreateUserAsync("booking.mikko", "mikko.salo@proseware-freight.example", pw, "Mikko Salo", new[] { Roles.Booking });
        await CreateUserAsync("nwf.portal", "logistics@northwind-foods.example", pw, "Northwind Foods logistics", new[] { Roles.Customer }, customerId: 1);
        await CreateUserAsync("wwi.portal", "import@wideworldimporters.example", pw, "Wide World Importers import desk", new[] { Roles.Customer }, customerId: 5);
    }
}

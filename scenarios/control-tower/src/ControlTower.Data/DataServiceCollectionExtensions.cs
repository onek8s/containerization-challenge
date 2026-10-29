using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Proseware.ControlTower.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the database. "Database:Provider" selects SqlServer (default) or Sqlite;
    /// the connection string is "ConnectionStrings:ControlTower".
    /// </summary>
    public static IServiceCollection AddControlTowerData(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "SqlServer";
        var connectionString = configuration.GetConnectionString("ControlTower")
            ?? throw new InvalidOperationException("Connection string 'ControlTower' is missing.");

        services.AddDbContext<ControlTowerDbContext>(options =>
        {
            if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.UseNetTopologySuite();
                    sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
                    sql.CommandTimeout(60);
                });
            }
        });
        services.AddScoped<ShipmentNumberAllocator>();
        services.AddScoped<ShipmentQueries>();
        return services;
    }

    /// <summary>Creates and seeds the development database when it does not exist yet.</summary>
    public static async Task EnsureDevelopmentDatabaseAsync(this IServiceProvider services, bool seed)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ControlTowerDbContext>();
        if (!db.IsSqlite) return;
        if (await db.Database.EnsureCreatedAsync() && seed)
            await new Seed.SeedData(db, DateTimeOffset.UtcNow).RunAsync();
    }
}

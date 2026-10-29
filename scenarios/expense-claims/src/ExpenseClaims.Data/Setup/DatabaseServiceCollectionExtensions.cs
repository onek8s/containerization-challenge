using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseClaims.Data.Setup;

public static class DatabaseServiceCollectionExtensions
{
    /// <summary>
    /// Registers the database context. <c>Database:Provider</c> is <c>SqlServer</c> (default) or
    /// <c>Sqlite</c> for local development without SQL Server.
    /// </summary>
    public static IServiceCollection AddExpenseClaimsDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "SqlServer";
        var connection = configuration.GetConnectionString("ExpenseClaims")
            ?? throw new InvalidOperationException("Connection string 'ExpenseClaims' is not configured.");

        services.AddDbContext<ExpenseClaimsDbContext>(options =>
        {
            if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
                options.UseSqlite(connection);
            else
                options.UseSqlServer(connection, sql =>
                {
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", "exp");
                    sql.EnableRetryOnFailure(3);
                });
        });
        return services;
    }

    /// <summary>Applies pending migrations, or creates the schema on SQLite.</summary>
    public static async Task MigrateExpenseClaimsDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExpenseClaimsDbContext>();
        if (db.Database.IsSqlite())
        {
            var file = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
            var folder = Path.GetDirectoryName(Path.GetFullPath(file));
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);
            await db.Database.EnsureCreatedAsync();
        }
        else
            await db.Database.MigrateAsync();
    }
}

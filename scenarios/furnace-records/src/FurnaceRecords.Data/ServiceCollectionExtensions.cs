using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FurnaceRecords.Data;

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    /// <summary><c>SqlServer</c> or <c>Sqlite</c>.</summary>
    public string Provider { get; set; } = "SqlServer";

    public bool SeedSampleData { get; set; }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFurnaceRecordsData(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(DatabaseOptions.Section).Get<DatabaseOptions>() ?? new DatabaseOptions();
        var connectionString = configuration.GetConnectionString("FurnaceRecords")
            ?? throw new InvalidOperationException("Connection string 'FurnaceRecords' is not configured.");

        services.AddSingleton(options);
        services.AddDbContextFactory<FurnaceRecordsContext>(o =>
        {
            if (string.Equals(options.Provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                o.UseSqlite(connectionString);
            }
            else
            {
                o.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
            }
        });
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<FurnaceRecordsContext>>().CreateDbContext());
        services.AddScoped<LoadService>();
        services.AddScoped<ApiKeyService>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}

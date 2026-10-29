using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Humongous.Quoting.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddQuotingData(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
        services.AddSingleton(options);

        var connectionString = configuration.GetConnectionString("Quoting")
            ?? throw new InvalidOperationException("Connection string 'Quoting' is missing.");

        services.AddDbContext<QuotingDbContext>(db =>
        {
            if (string.Equals(options.Provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
                db.UseSqlite(connectionString);
            else
                db.UseSqlServer(connectionString, sql =>
                {
                    sql.EnableRetryOnFailure(3);
                    sql.CommandTimeout(60);
                });
        });

        services.AddScoped<NumberGenerator>();
        services.AddScoped<QuotingQueries>();
        services.AddScoped<DatabaseInitializer>();
        return services;
    }
}

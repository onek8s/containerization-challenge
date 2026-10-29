using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolicyChanges.Core.Requests;
using PolicyChanges.Data.Services;

namespace PolicyChanges.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPolicyChangesData(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PolicyChanges")
            ?? throw new InvalidOperationException("Connection string PolicyChanges is missing.");
        var provider = configuration["Database:Provider"] ?? "SqlServer";

        services.AddDbContext<PolicyChangesContext>(o =>
        {
            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
                o.UseSqlite(connectionString);
            else
                o.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3));
        });

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ChangeValidator>();
        services.Configure<QueueOptions>(configuration.GetSection(QueueOptions.Section));
        services.AddScoped<ChangeService>();
        services.AddScoped<RequestQueue>();
        return services;
    }
}

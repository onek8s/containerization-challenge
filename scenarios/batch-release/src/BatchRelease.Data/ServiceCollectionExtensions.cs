using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BatchRelease.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBatchReleaseData(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("BatchRelease")
            ?? throw new InvalidOperationException("Connection string 'BatchRelease' is not configured.");

        services.AddDbContext<BatchReleaseContext>(o => o.UseSqlServer(connectionString, sql =>
        {
            sql.EnableRetryOnFailure(3);
            sql.CommandTimeout(60);
        }));
        services.AddScoped<BatchQueries>();
        services.AddScoped<BatchIntake>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}

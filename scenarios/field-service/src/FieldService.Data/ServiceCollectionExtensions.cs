using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FieldService.Data.Services;

namespace FieldService.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFieldServiceData(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "SqlServer";
        var connectionString = configuration.GetConnectionString("FieldService")
            ?? throw new InvalidOperationException("Connection string 'FieldService' is not configured.");

        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var dataSource = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString).DataSource;
            var folder = Path.GetDirectoryName(Path.GetFullPath(dataSource));
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }

        services.AddDbContext<FieldServiceContext>(options =>
        {
            if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3));
            }
        });

        services.AddScoped<WorkOrderService>();
        services.AddScoped<NumberSequence>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}

using FreightRates.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreightRates.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFreightRatesData(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "SqlServer";
        var connectionString = configuration.GetConnectionString("FreightRates")
            ?? throw new InvalidOperationException("Connection string 'FreightRates' is not configured.");

        services.AddDbContext<FreightRatesContext>(options =>
        {
            if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
                options.UseSqlite(connectionString);
            else
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
        });

        services.AddScoped<QuoteService>();
        services.AddScoped<CustomerService>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}

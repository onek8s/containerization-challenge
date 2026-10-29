using System;
using Fabrikam.Housing.Data.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fabrikam.Housing.Data
{
    public static class DataServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the housing database. Database:Provider is SqlServer (default) or Sqlite for development.
        /// </summary>
        public static IServiceCollection AddHousingData(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("Housing")
                ?? throw new InvalidOperationException("ConnectionStrings:Housing is not configured.");
            var provider = configuration["Database:Provider"] ?? "SqlServer";

            services.AddSingleton(new ConnectionFactory(provider, connectionString));
            services.AddDbContext<HousingDbContext>(options =>
            {
                if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
                    options.UseSqlite(connectionString);
                else
                    options.UseSqlServer(connectionString, sql =>
                    {
                        sql.EnableRetryOnFailure(3);
                        sql.CommandTimeout(60);
                    });
            });
            services.AddScoped<CounterService>();
            services.AddScoped<AuditWriter>();
            services.AddScoped<ReportQueries>();
            services.AddScoped<RentAccountQueries>();
            return services;
        }
    }
}

using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ProductReturns.Data
{
    public static class DatabaseSetup
    {
        /// <summary>
        /// Registers the database context. Provider "SqlServer" (default) or "Sqlite" for local development.
        /// </summary>
        public static IServiceCollection AddReturnsDatabase(this IServiceCollection services, string provider, string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Connection string 'Returns' is not configured.");
            }

            services.AddDbContext<ReturnsDbContext>(options =>
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

            services.AddScoped<ReturnsRepository>();
            return services;
        }

        /// <summary>Creates the SQLite development database with sample ERP data.</summary>
        public static void EnsureDevelopmentDatabase(IServiceProvider services)
        {
            using (var scope = services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ReturnsDbContext>();
                if (!db.Database.IsSqlite())
                {
                    return;
                }

                var file = new SqliteConnectionStringBuilder(db.Database.GetDbConnection().ConnectionString).DataSource;
                var folder = Path.GetDirectoryName(Path.GetFullPath(file));
                if (!string.IsNullOrEmpty(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                if (db.Database.EnsureCreated())
                {
                    SampleData.Load(db);
                }
            }
        }
    }
}

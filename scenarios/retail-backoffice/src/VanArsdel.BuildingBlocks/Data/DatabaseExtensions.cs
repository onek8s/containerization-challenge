using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VanArsdel.BuildingBlocks.Data
{
    public static class DatabaseExtensions
    {
        /// <summary>
        /// Registers the service DbContext. "Database:Provider" is SqlServer (default) or Sqlite
        /// (developer machines and the build agents).
        /// </summary>
        public static IServiceCollection AddServiceDatabase<TContext>(this IServiceCollection services, IConfiguration configuration,
            string connectionName) where TContext : DbContext
        {
            var provider = DatabaseProvider(configuration);
            var connectionString = configuration.GetConnectionString(connectionName);
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException($"ConnectionStrings:{connectionName} is not configured.");
            }

            services.AddDbContext<TContext>(options => Configure(options, provider, connectionString, typeof(TContext).Assembly.GetName().Name));
            return services;
        }

        public static void Configure(DbContextOptionsBuilder options, string provider, string connectionString, string migrationsAssembly = null)
        {
            if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                EnsureSqliteFolder(connectionString);
                options.UseSqlite(connectionString);
            }
            else
            {
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
                    if (migrationsAssembly != null) sql.MigrationsAssembly(migrationsAssembly);
                });
            }
        }

        public static string DatabaseProvider(IConfiguration configuration) =>
            configuration["Database:Provider"] ?? "SqlServer";

        public static bool IsSqlite(IConfiguration configuration) =>
            string.Equals(DatabaseProvider(configuration), "Sqlite", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Handles "--print-schema [file]" (writes the CREATE script used for db\*.sql) and creates the
        /// schema when Database:AutoCreate is set (developer machines only; production uses the scripts).
        /// Returns false when the process should exit.
        /// </summary>
        public static bool PrepareDatabase<TContext>(this IHost host, string[] args, Func<TContext, IServiceProvider, Task> seed = null)
            where TContext : DbContext
        {
            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TContext>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Database");

            if (Hosting.VanArsdelHost.HasSwitch(args, "--print-schema"))
            {
                var script = db.Database.GenerateCreateScript();
                var target = Hosting.VanArsdelHost.SwitchValue(args, "--print-schema");
                if (!string.IsNullOrEmpty(target) && !target.StartsWith("--", StringComparison.Ordinal))
                {
                    File.WriteAllText(target, script);
                }
                else
                {
                    Console.WriteLine(script);
                }
                return false;
            }

            if (configuration.GetValue("Database:AutoCreate", false))
            {
                var created = db.Database.EnsureCreated();
                logger.LogInformation("Database {Database} ready (created: {Created})", typeof(TContext).Name, created);
                if (seed != null)
                {
                    seed(db, scope.ServiceProvider).GetAwaiter().GetResult();
                }
            }
            return true;
        }

        private static void EnsureSqliteFolder(string connectionString)
        {
            const string key = "Data Source=";
            var i = connectionString.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return;
            var path = connectionString.Substring(i + key.Length).Split(';')[0].Trim();
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }
    }
}

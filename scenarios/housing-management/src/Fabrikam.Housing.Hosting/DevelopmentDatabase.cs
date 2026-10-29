using System;
using System.IO;
using System.Threading;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Data.Seed;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fabrikam.Housing.Hosting
{
    /// <summary>
    /// Development only: creates and seeds the SQLite database. Production databases are created with the
    /// scripts in the db folder.
    /// </summary>
    public static class DevelopmentDatabase
    {
        private static readonly object Gate = new object();

        public static void EnsureSeeded(IServiceProvider services)
        {
            var configuration = services.GetRequiredService<IConfiguration>();
            if (!string.Equals(configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase)) return;

            lock (Gate)
            using (SeedLock(configuration.GetConnectionString("Housing")))
            {
                using (var scope = services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<HousingDbContext>();
                    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DevelopmentDatabase");
                    db.Database.EnsureCreated();
                    if (DevelopmentSeeder.IsSeeded(db)) return;
                    logger.LogInformation("Seeding development database");
                    var today = scope.ServiceProvider.GetRequiredService<IClock>().Today;
                    new DevelopmentSeeder(db, today).Seed();
                }
            }
        }

        /// <summary>
        /// The applications are usually started together; a lock file next to the database stops two of them
        /// creating the tables at the same time.
        /// </summary>
        private static IDisposable SeedLock(string connectionString)
        {
            var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
            var lockFile = Path.GetFullPath(dataSource) + ".lock";
            Directory.CreateDirectory(Path.GetDirectoryName(lockFile));
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    return new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
                }
                catch (IOException) when (attempt < 600)
                {
                    Thread.Sleep(500);
                }
            }
        }
    }
}

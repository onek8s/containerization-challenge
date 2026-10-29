using System;
using System.IO;
using System.Linq;
using System.Threading;
using Fabrikam.Housing.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Web;

namespace Fabrikam.Housing.DocumentService
{
    /// <summary>
    /// The generic host (Kestrel API, intake watcher, nightly re-index) wrapped for Topshelf start/stop.
    /// </summary>
    public class DocumentServiceHost
    {
        private readonly string[] _args;
        private IHost _host;

        public DocumentServiceHost(string[] args)
        {
            _args = args;
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                // Services start in System32; settings and nlog.config are next to the executable.
                .UseContentRoot(AppContext.BaseDirectory)
                .ConfigureWebHostDefaults(web => web.UseStartup<Startup>())
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(LogLevel.Trace);
                })
                .UseNLog();
        }

        public void Start()
        {
            _host = CreateHostBuilder(_args).Build();
            WaitForDatabase(_host.Services);
            _host.StartAsync().GetAwaiter().GetResult();
        }

        public void Stop()
        {
            if (_host == null) return;
            _host.StopAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            _host.Dispose();
            _host = null;
        }

        public void RunConsole()
        {
            using (var host = CreateHostBuilder(_args).Build())
            {
                WaitForDatabase(host.Services);
                host.Run();
            }
        }

        /// <summary>
        /// The housing database is created by the database scripts (or, in development, seeded by the back office).
        /// The service waits for it rather than failing at start-up.
        /// </summary>
        private static void WaitForDatabase(IServiceProvider services)
        {
            var logger = services.GetRequiredService<ILogger<DocumentServiceHost>>();
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    using (var scope = services.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<HousingDbContext>();
                        db.Documents.AsNoTracking().Take(1).Load();
                        return;
                    }
                }
                catch (Exception ex) when (attempt < 60)
                {
                    logger.LogWarning("Housing database not available yet ({Message}), retrying in 10 seconds", ex.GetBaseException().Message);
                    Thread.Sleep(TimeSpan.FromSeconds(10));
                }
            }
        }
    }
}

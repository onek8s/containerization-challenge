using System;
using Fabrikam.Housing.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Web;
using Unity;
using Unity.Microsoft.DependencyInjection;

namespace Fabrikam.Housing.Hosting
{
    /// <summary>
    /// Common start-up for the Fabrikam Housing web applications: NLog (nlog.config next to the application),
    /// the Unity container and IIS in-process hosting.
    /// </summary>
    public static class HousingHost
    {
        public static int Run<TStartup>(string[] args, string applicationName) where TStartup : class
        {
            var logger = NLogBuilder.ConfigureNLog("nlog.config").GetCurrentClassLogger();
            AuditWriter.ApplicationName = applicationName;
            try
            {
                logger.Info("Starting {0}", applicationName);
                CreateHostBuilder<TStartup>(args).Build().Run();
                return 0;
            }
            catch (Exception ex)
            {
                logger.Fatal(ex, "{0} stopped because of an exception", applicationName);
                return 1;
            }
            finally
            {
                NLog.LogManager.Shutdown();
            }
        }

        public static IHostBuilder CreateHostBuilder<TStartup>(string[] args) where TStartup : class
        {
            return Host.CreateDefaultBuilder(args)
                .UseUnityServiceProvider(new UnityContainer())
                .ConfigureWebHostDefaults(web =>
                {
                    web.UseStartup<TStartup>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(LogLevel.Trace);
                })
                .UseNLog();
        }
    }
}

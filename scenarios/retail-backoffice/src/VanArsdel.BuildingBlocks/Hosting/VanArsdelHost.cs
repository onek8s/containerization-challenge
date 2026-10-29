using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace VanArsdel.BuildingBlocks.Hosting
{
    /// <summary>
    /// Common entry point: bootstrap logger, Serilog from configuration, fatal error logging.
    /// </summary>
    public static class VanArsdelHost
    {
        public static int Run(string appName, string[] args, Func<string[], IHostBuilder> createHostBuilder,
            Func<IHost, string[], bool> beforeRun = null, Func<string[], IHost> buildHost = null)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.WithProperty("Application", appName)
                .WriteTo.Console()
                .CreateBootstrapLogger();

            try
            {
                Log.Information("Starting {Application} {Version}", appName,
                    typeof(VanArsdelHost).Assembly.GetName().Version);
                var host = buildHost != null ? buildHost(args) : createHostBuilder(args).Build();
                if (beforeRun != null && !beforeRun(host, args))
                {
                    return 0;
                }
                host.Run();
                return 0;
            }
            catch (Exception ex) when (ex.GetType().Name != "StopTheHostException" && ex.GetType().Name != "HostAbortedException")
            {
                Log.Fatal(ex, "{Application} terminated unexpectedly", appName);
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

#if NET6_0_OR_GREATER
        /// <summary>Same as <see cref="Run"/> for services on the .NET 6 minimal hosting model.</summary>
        public static int RunWeb(string appName, string[] args, Func<string[], Microsoft.AspNetCore.Builder.WebApplication> build,
            Func<IHost, string[], bool> beforeRun = null)
        {
            return Run(appName, args, null, beforeRun, build);
        }
#endif

        /// <summary>
        /// Serilog from the "Serilog" section. The file sink path can be moved with the
        /// VANARSDEL_LOG_DIR environment variable (used by the build agents).
        /// </summary>
        public static IHostBuilder UseVanArsdelSerilog(this IHostBuilder builder, string appName)
        {
            return builder.UseSerilog((context, services, logger) =>
            {
                logger.ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .Enrich.WithMachineName()
                    .Enrich.WithProperty("Application", appName)
                    .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName);

                var logDir = Environment.GetEnvironmentVariable("VANARSDEL_LOG_DIR");
                if (!string.IsNullOrEmpty(logDir))
                {
                    Directory.CreateDirectory(logDir);
                    logger.WriteTo.File(Path.Combine(logDir, appName + "-.log"),
                        rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14,
                        restrictedToMinimumLevel: LogEventLevel.Information);
                }
            });
        }

        /// <summary>
        /// Lets an operator run "--print-schema" and similar maintenance switches before the host starts.
        /// </summary>
        public static bool HasSwitch(string[] args, string name) =>
            args != null && args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

        public static string SwitchValue(string[] args, string name)
        {
            if (args == null) return null;
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            }
            return null;
        }

        public static T GetOptions<T>(this IConfiguration configuration, string section) where T : new()
        {
            var options = new T();
            configuration.GetSection(section).Bind(options);
            return options;
        }

        public static IServiceCollection AddOptionsFrom<T>(this IServiceCollection services, IConfiguration configuration, string section)
            where T : class
        {
            services.Configure<T>(configuration.GetSection(section));
            return services;
        }
    }
}

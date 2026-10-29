using System;
using System.IO;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Litware.OrderHub.Portal
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "create-user")
            {
                return UserCommand.Run(args);
            }

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Production.json", optional: true)
                .AddEnvironmentVariables("ORDERHUB_")
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.WithProperty("Service", "Portal")
                .WriteTo.File(Path.Combine(configuration["Logging:Folder"] ?? @"C:\OrderHub\Logs", "portal-.log"),
                    rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
                .CreateLogger();

            try
            {
                BuildWebHost(args, configuration).Run();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Portal stopped");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IWebHost BuildWebHost(string[] args, IConfiguration configuration)
        {
            return WebHost.CreateDefaultBuilder(args)
                .UseContentRoot(AppContext.BaseDirectory)
                .UseConfiguration(configuration)
                .UseSerilog()
                .UseStartup<Startup>()
                .Build();
        }
    }
}

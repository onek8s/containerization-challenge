using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using VanArsdel.PosFeed.Hosting;
using VanArsdel.PosFeed.Sources;

namespace VanArsdel.PosFeed
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var isService = !(Debugger.IsAttached || args.Contains("--console"));
            if (isService)
            {
                // Services start in System32.
                Directory.SetCurrentDirectory(AppContext.BaseDirectory);
            }

            var builder = new HostBuilder()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .ConfigureHostConfiguration(config =>
                {
                    config.AddEnvironmentVariables("ASPNETCORE_");
                    config.AddEnvironmentVariables("DOTNET_");
                })
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.SetBasePath(AppContext.BaseDirectory);
                    config.AddJsonFile("appsettings.json", optional: false);
                    config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
                    config.AddEnvironmentVariables();
                    config.AddCommandLine(args.Where(a => a != "--console").ToArray());
                })
                .UseSerilog((context, logger) =>
                {
                    logger.ReadFrom.Configuration(context.Configuration).Enrich.FromLogContext().Enrich.WithProperty("Application", "posfeed");
                    var dir = Environment.GetEnvironmentVariable("VANARSDEL_LOG_DIR");
                    if (!string.IsNullOrEmpty(dir))
                    {
                        logger.WriteTo.File(Path.Combine(dir, "posfeed-.log"), rollingInterval: RollingInterval.Day);
                    }
                })
                .ConfigureServices((context, services) =>
                {
                    services.Configure<PosFeedOptions>(context.Configuration.GetSection("PosFeed"));
                    var bus = context.Configuration.GetSection("Messaging").Get<BusOptions>() ?? new BusOptions();
                    services.AddSingleton(BusFactory.Create(bus));
                    services.AddSingleton<IBus>(sp => sp.GetRequiredService<IBusControl>());
                    services.AddSingleton<IHostedService, BusService>();

                    var source = context.Configuration["PosFeed:Source"] ?? "Folder";
                    if (string.Equals(source, "Sftp", StringComparison.OrdinalIgnoreCase))
                    {
                        services.AddSingleton<IPosFileSource, SftpSource>();
                    }
                    else
                    {
                        services.AddSingleton<IPosFileSource, FolderSource>();
                    }
                    services.AddSingleton<IHostedService, PosFeedWorker>();
                });

            try
            {
                if (isService)
                {
                    await builder.RunAsServiceAsync();
                }
                else
                {
                    await builder.RunConsoleAsync();
                }
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "POS feed stopped");
                Console.Error.WriteLine(ex);
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}

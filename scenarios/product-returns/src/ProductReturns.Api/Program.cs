using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProductReturns.Data;

namespace ProductReturns.Api
{
    public class Program
    {
        /// <summary>Machine settings maintained by operations, outside the install folder.</summary>
        public const string MachineSettingsFile = @"C:\ProgramData\WideWorldImporters\ProductReturns\api.settings.json";

        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            if (host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
            {
                DatabaseSetup.EnsureDevelopmentDatabase(host.Services);
            }

            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureAppConfiguration((context, config) =>
                {
                    if (File.Exists(MachineSettingsFile))
                    {
                        config.AddJsonFile(MachineSettingsFile, optional: true, reloadOnChange: true);
                    }

                    config.AddCommandLine(args);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}

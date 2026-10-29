using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ProductReturns.Portal
{
    public class Program
    {
        /// <summary>Machine settings maintained by operations, outside the install folder.</summary>
        public const string MachineSettingsFile = @"C:\ProgramData\WideWorldImporters\ProductReturns\portal.settings.json";

        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
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

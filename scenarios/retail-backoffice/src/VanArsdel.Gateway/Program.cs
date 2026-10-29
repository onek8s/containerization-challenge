using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using VanArsdel.BuildingBlocks.Hosting;

namespace VanArsdel.Gateway
{
    public class Program
    {
        public static int Main(string[] args) => VanArsdelHost.Run("gateway", args, CreateHostBuilder);

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    var env = context.HostingEnvironment.EnvironmentName;
                    config.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
                          .AddJsonFile($"ocelot.{env}.json", optional: true, reloadOnChange: true)
                          .AddEnvironmentVariables();
                })
                .UseVanArsdelSerilog("gateway")
                .ConfigureWebHostDefaults(web => web.UseStartup<Startup>());
    }
}

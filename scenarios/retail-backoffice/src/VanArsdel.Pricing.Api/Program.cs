using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VanArsdel.BuildingBlocks.Hosting;
using VanArsdel.Pricing.Api.Data;

namespace VanArsdel.Pricing.Api
{
    public class Program
    {
        public static int Main(string[] args) =>
            VanArsdelHost.Run("pricing-api", args, CreateHostBuilder, (host, a) =>
            {
                var configuration = host.Services.GetRequiredService<IConfiguration>();
                var factory = host.Services.GetRequiredService<DbConnectionFactory>();
                if (configuration.GetValue("Database:AutoCreate", false) && factory.IsSqlite)
                {
                    factory.CreateSqliteSchema();
                }
                return true;
            });

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseVanArsdelSerilog("pricing-api")
                .ConfigureWebHostDefaults(web => web.UseStartup<Startup>());
    }
}

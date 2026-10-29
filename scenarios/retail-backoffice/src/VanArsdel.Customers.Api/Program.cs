using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VanArsdel.BuildingBlocks.Data;
using VanArsdel.BuildingBlocks.Hosting;
using VanArsdel.Customers.Api.Data;

namespace VanArsdel.Customers.Api
{
    public class Program
    {
        public static int Main(string[] args) =>
            VanArsdelHost.Run("customers-api", args, CreateHostBuilder, (host, a) =>
            {
                var provider = host.Services.GetRequiredService<IConfiguration>()["DocumentStore:Provider"];
                return !string.Equals(provider, "Sql", StringComparison.OrdinalIgnoreCase) || host.PrepareDatabase<CustomersDbContext>(a);
            });

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseVanArsdelSerilog("customers-api")
                .ConfigureWebHostDefaults(web => web.UseStartup<Startup>());
    }
}

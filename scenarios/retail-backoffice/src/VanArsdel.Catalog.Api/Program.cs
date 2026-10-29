using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using VanArsdel.BuildingBlocks.Data;
using VanArsdel.BuildingBlocks.Hosting;
using VanArsdel.Catalog.Api.Data;

namespace VanArsdel.Catalog.Api
{
    public class Program
    {
        public static int Main(string[] args) =>
            VanArsdelHost.Run("catalog-api", args, CreateHostBuilder,
                (host, a) => host.PrepareDatabase<CatalogDbContext>(a, (db, sp) => CatalogSeed.SeedAsync(db)));

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseVanArsdelSerilog("catalog-api")
                .ConfigureWebHostDefaults(web => web.UseStartup<Startup>());
    }
}

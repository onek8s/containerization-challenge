using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using VanArsdel.BuildingBlocks.Data;
using VanArsdel.BuildingBlocks.Hosting;
using VanArsdel.StoreOps.Api.Data;

namespace VanArsdel.StoreOps.Api
{
    public class Program
    {
        public static int Main(string[] args) =>
            VanArsdelHost.Run("storeops-api", args, CreateHostBuilder,
                (host, a) => host.PrepareDatabase<StoreOpsDbContext>(a));

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseVanArsdelSerilog("storeops-api")
                .ConfigureWebHostDefaults(web => web.UseStartup<Startup>());
    }
}

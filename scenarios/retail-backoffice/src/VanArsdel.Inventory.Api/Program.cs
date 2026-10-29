using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using VanArsdel.BuildingBlocks.Data;
using VanArsdel.BuildingBlocks.Hosting;
using VanArsdel.Inventory.Api.Data;

namespace VanArsdel.Inventory.Api
{
    public class Program
    {
        public static int Main(string[] args) =>
            VanArsdelHost.Run("inventory-api", args, CreateHostBuilder, (host, a) => host.PrepareDatabase<InventoryDbContext>(a));

        // Runs as a Windows service with Kestrel (not IIS) because gRPC needs HTTP/2 trailers,
        // which IIS on Windows Server 2019 does not support. Endpoints and certificate: "Kestrel" section.
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService(o => o.ServiceName = "VanArsdelInventory")
                .UseVanArsdelSerilog("inventory-api")
                .ConfigureWebHostDefaults(web => web.UseStartup<Startup>());
    }
}

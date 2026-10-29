using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using VanArsdel.Allocation.Api.Data;
using VanArsdel.BuildingBlocks.Data;
using VanArsdel.BuildingBlocks.Hosting;

namespace VanArsdel.Allocation.Api
{
    public class Program
    {
        public static int Main(string[] args) =>
            VanArsdelHost.Run("allocation-api", args, CreateHostBuilder, (host, a) => host.PrepareDatabase<AllocationDbContext>(a, (db, sp) =>
            {
                AllocationDbContext.Seed(db);
                return System.Threading.Tasks.Task.CompletedTask;
            }));

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseVanArsdelSerilog("allocation-api")
                .ConfigureWebHostDefaults(web => web.UseStartup<Startup>());
    }
}

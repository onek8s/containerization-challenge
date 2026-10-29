using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace VanArsdel.StoreManager
{
    public class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                CreateHostBuilder(args).Build().Run();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Store manager host stopped");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog((context, logger) => logger
                    .ReadFrom.Configuration(context.Configuration)
                    .Enrich.WithProperty("Application", "storemanager")
                    .WriteTo.Console())
                .ConfigureWebHostDefaults(web => web.UseStartup<Startup>());
    }
}

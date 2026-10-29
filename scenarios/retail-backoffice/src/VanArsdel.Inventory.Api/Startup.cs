using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VanArsdel.BuildingBlocks.Caching;
using VanArsdel.BuildingBlocks.Data;
using VanArsdel.BuildingBlocks.Messaging;
using VanArsdel.BuildingBlocks.Outbox;
using VanArsdel.BuildingBlocks.Web;
using VanArsdel.Inventory.Api.Consumers;
using VanArsdel.Inventory.Api.Data;
using VanArsdel.Inventory.Api.Services;
using VanArsdel.Inventory.Grpc;

namespace VanArsdel.Inventory.Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddVanArsdelApi(Configuration, "VanArsdel Inventory API", typeof(Startup).Assembly, Policies.Configure);
            services.AddGrpc(o => o.EnableDetailedErrors = Configuration.GetValue("Grpc:DetailedErrors", false));
            services.AddGrpcReflection();
            services.AddServiceDatabase<InventoryDbContext>(Configuration, "Inventory");
            services.AddVanArsdelCaching(Configuration, "inventory");
            services.AddVanArsdelMessaging(Configuration, "inventory", x =>
            {
                x.AddConsumer<StoreConsumer>();
                x.AddConsumer<ItemConsumer>();
                x.AddConsumer<StoreSalesConsumer>();
                x.AddConsumer<GoodsReceivedConsumer>();
                x.AddConsumer<ReturnRestockedConsumer>();
            });
            services.AddEfOutbox<InventoryDbContext>();
            services.AddScoped<StockService>();
            services.AddScoped<AvailabilityQueries>();
            services.AddScoped<ReplenishmentService>();
            services.AddHostedService<ReservationExpiryService>();
            services.AddHealthChecks().AddDbContextCheck<InventoryDbContext>("database");
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseVanArsdelApi(env, e =>
            {
                e.MapGrpcService<InventoryGrpcService>();
                if (env.IsDevelopment()) e.MapGrpcReflectionService();
            });
        }
    }
}

using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using VanArsdel.BuildingBlocks.Caching;
using VanArsdel.BuildingBlocks.Data;
using VanArsdel.BuildingBlocks.Messaging;
using VanArsdel.BuildingBlocks.Outbox;
using VanArsdel.BuildingBlocks.Web;
using VanArsdel.StoreOps.Api.Consumers;
using VanArsdel.StoreOps.Api.Data;
using VanArsdel.StoreOps.Api.Hubs;
using VanArsdel.StoreOps.Api.Services;

namespace VanArsdel.StoreOps.Api
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
            services.AddVanArsdelApi(Configuration, "VanArsdel Store Operations API", typeof(Startup).Assembly, Policies.Configure);
            services.AddServiceDatabase<StoreOpsDbContext>(Configuration, "StoreOps");
            services.AddVanArsdelCaching(Configuration, "storeops");
            services.AddVanArsdelMessaging(Configuration, "storeops", x =>
            {
                x.AddConsumer<FulfilmentConsumers>();
                x.AddConsumer<StoreSalesConsumer>();
            });
            services.AddEfOutbox<StoreOpsDbContext>();

            // Two IIS servers behind the load balancer, so hub messages go through the Redis backplane.
            var signalR = services.AddSignalR();
            var redis = Configuration["Redis:Configuration"];
            if (!string.IsNullOrEmpty(redis))
            {
                signalR.AddStackExchangeRedis(redis, o => o.Configuration.ChannelPrefix = "storeops");
            }
            services.AddSingleton<StoreNotifier>();

            services.AddCors(o => o.AddPolicy("stores", p => p
                .WithOrigins(ApiExtensions.ConfiguredCorsOrigins(Configuration).ToArray())
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));

            services.AddHealthChecks()
                .AddDbContextCheck<StoreOpsDbContext>("database");
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseCors("stores");
            app.UseVanArsdelApi(env, e => e.MapHub<StoreHub>("/hubs/store"));
        }
    }
}

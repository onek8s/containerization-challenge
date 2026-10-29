using System;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VanArsdel.Allocation.Api.Consumers;
using VanArsdel.Allocation.Api.Data;
using VanArsdel.Allocation.Api.Services;
using VanArsdel.BuildingBlocks.Data;
using VanArsdel.BuildingBlocks.Http;
using VanArsdel.BuildingBlocks.Messaging;
using VanArsdel.BuildingBlocks.Security;
using VanArsdel.BuildingBlocks.Web;
using VanArsdel.Inventory.Grpc;

namespace VanArsdel.Allocation.Api
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
            services.AddVanArsdelApi(Configuration, "VanArsdel Allocation API", typeof(Startup).Assembly, Policies.Configure);
            services.AddServiceDatabase<AllocationDbContext>(Configuration, "Allocation");
            services.AddServiceIdentity(Configuration);
            services.AddGrpcClient<InventoryService.InventoryServiceClient>(o =>
                {
                    o.Address = new Uri(Configuration["Services:InventoryGrpc"]);
                })
                .AddHttpMessageHandler<ClientCredentialsHandler>();
            services.AddScoped<AllocationService>();
            services.AddVanArsdelMessaging(Configuration, "allocation", x =>
            {
                x.AddConsumer<AllocateOrderConsumer>();
                x.AddConsumer<ReservationConsumers>();
            }, (context, cfg) => cfg.UseInMemoryOutbox());
            services.AddHealthChecks().AddDbContextCheck<AllocationDbContext>("database");
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseVanArsdelApi(env);
        }
    }
}

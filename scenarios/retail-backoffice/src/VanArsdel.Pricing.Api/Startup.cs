using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using VanArsdel.BuildingBlocks.Caching;
using VanArsdel.BuildingBlocks.Messaging;
using VanArsdel.BuildingBlocks.Web;
using VanArsdel.Pricing.Api.Consumers;
using VanArsdel.Pricing.Api.Data;
using VanArsdel.Pricing.Api.Domain;

namespace VanArsdel.Pricing.Api
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
            DapperConfig.Register();
            services.AddVanArsdelApi(Configuration, "VanArsdel Pricing API", typeof(Startup).Assembly, Policies.Configure);
            services.AddSingleton<DbConnectionFactory>();
            services.AddSingleton<IDbConnectionFactory>(sp => sp.GetRequiredService<DbConnectionFactory>());
            services.AddScoped<PriceRepository>();
            services.AddScoped<PriceListRepository>();
            services.AddScoped<PromotionRepository>();
            services.AddScoped<PricingService>();
            services.AddVanArsdelCaching(Configuration, "pricing");
            services.AddVanArsdelMessaging(Configuration, "pricing", x =>
            {
                x.AddConsumer<ProductPublishedConsumer>();
                x.AddConsumer<ProductDiscontinuedConsumer>();
            });
            services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseVanArsdelApi(env);
        }
    }

    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly IDbConnectionFactory _db;

        public DatabaseHealthCheck(IDbConnectionFactory db)
        {
            _db = db;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                using var c = _db.Open();
                await c.ExecuteScalarAsync<int>("SELECT 1");
                return HealthCheckResult.Healthy();
            }
            catch (System.Exception ex)
            {
                return HealthCheckResult.Unhealthy("Database not reachable", ex);
            }
        }
    }
}

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using VanArsdel.BuildingBlocks.Caching;
using VanArsdel.BuildingBlocks.Data;
using VanArsdel.BuildingBlocks.Messaging;
using VanArsdel.BuildingBlocks.Outbox;
using VanArsdel.BuildingBlocks.Web;
using VanArsdel.Catalog.Api.Data;
using VanArsdel.Catalog.Api.Search;

namespace VanArsdel.Catalog.Api
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
            services.AddVanArsdelApi(Configuration, "VanArsdel Catalog API", typeof(Startup).Assembly, Policies.Configure);
            services.AddServiceDatabase<CatalogDbContext>(Configuration, "Catalog");
            services.AddVanArsdelCaching(Configuration, "catalog");
            services.AddVanArsdelMessaging(Configuration, "catalog");
            services.AddEfOutbox<CatalogDbContext>();

            var search = Configuration.GetSection("Search");
            if (string.Equals(search["Provider"], "Elasticsearch", StringComparison.OrdinalIgnoreCase))
            {
                var options = search.GetSection("Elasticsearch").Get<ElasticSearchOptions>() ?? new ElasticSearchOptions();
                services.AddSingleton(options);
                services.AddSingleton<IProductSearch, ElasticProductSearch>();
            }
            else
            {
                services.AddSingleton<IProductSearch, InMemoryProductSearch>();
                services.AddHostedService<SearchIndexWarmup>();
            }
            services.AddScoped<SearchIndexer>();

            services.AddHealthChecks()
                .AddDbContextCheck<CatalogDbContext>("database")
                .AddCheck<SearchHealthCheck>("search", HealthStatus.Degraded);
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseVanArsdelApi(env);
        }
    }

    public class SearchHealthCheck : IHealthCheck
    {
        private readonly IProductSearch _search;

        public SearchHealthCheck(IProductSearch search)
        {
            _search = search;
        }

        public async System.Threading.Tasks.Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, System.Threading.CancellationToken cancellationToken = default)
        {
            return await _search.PingAsync(cancellationToken)
                ? HealthCheckResult.Healthy(_search.GetType().Name)
                : HealthCheckResult.Degraded("Search cluster does not answer");
        }
    }
}

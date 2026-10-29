using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using VanArsdel.BuildingBlocks.Data;
using VanArsdel.BuildingBlocks.Messaging;
using VanArsdel.BuildingBlocks.Web;
using VanArsdel.Customers.Api.Consumers;
using VanArsdel.Customers.Api.Data;
using VanArsdel.Customers.Api.Documents;
using VanArsdel.Customers.Api.Domain;

namespace VanArsdel.Customers.Api
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
            services.AddVanArsdelApi(Configuration, "VanArsdel Customers API", typeof(Startup).Assembly, Policies.Configure);

            // MongoDB in production; LiteDB file or a SQL table where no MongoDB is available.
            var provider = Configuration["DocumentStore:Provider"] ?? "MongoDb";
            switch (provider.ToLowerInvariant())
            {
                case "litedb":
                    services.AddSingleton<ICustomerStore>(new LiteDbCustomerStore(Configuration["DocumentStore:LiteDbPath"]));
                    break;
                case "sql":
                    services.AddServiceDatabase<CustomersDbContext>(Configuration, "Customers");
                    services.AddScoped<ICustomerStore, SqlCustomerStore>();
                    break;
                default:
                    services.AddSingleton<ICustomerStore>(new MongoCustomerStore(Configuration.GetConnectionString("CustomersMongo"),
                        Configuration["DocumentStore:Database"] ?? "vanarsdel_customers"));
                    break;
            }

            services.AddScoped<CustomerService>();
            services.AddVanArsdelMessaging(Configuration, "customers", x => x.AddConsumer<LoyaltyConsumers>());
            services.AddHealthChecks().AddCheck<DocumentStoreHealthCheck>("document-store");
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseVanArsdelApi(env);
        }
    }

    public class DocumentStoreHealthCheck : IHealthCheck
    {
        private readonly ICustomerStore _store;

        public DocumentStoreHealthCheck(ICustomerStore store)
        {
            _store = store;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            await _store.PingAsync(cancellationToken) ? HealthCheckResult.Healthy(_store.GetType().Name) : HealthCheckResult.Unhealthy("Document store not reachable");
    }
}

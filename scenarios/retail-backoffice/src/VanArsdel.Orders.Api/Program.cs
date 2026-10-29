using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using VanArsdel.BuildingBlocks.Caching;
using VanArsdel.BuildingBlocks.Data;
using VanArsdel.BuildingBlocks.Hosting;
using VanArsdel.BuildingBlocks.Messaging;
using VanArsdel.BuildingBlocks.Outbox;
using VanArsdel.BuildingBlocks.Http;
using VanArsdel.BuildingBlocks.Web;
using VanArsdel.Integration.Carrier;
using VanArsdel.Orders.Api;
using VanArsdel.Orders.Api.Consumers;
using VanArsdel.Orders.Api.Data;
using VanArsdel.Orders.Api.Domain;
using VanArsdel.Orders.Api.Integration;
using VanArsdel.Orders.Api.Saga;

return VanArsdelHost.RunWeb("orders-api", args, a => OrdersApp.Build(a), (host, a) => host.PrepareDatabase<OrdersDbContext>(a));

public static class OrdersApp
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder> configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Host.UseVanArsdelSerilog("orders-api");
        configure?.Invoke(builder);
        var configuration = builder.Configuration;
        var services = builder.Services;

        services.AddVanArsdelApi(configuration, "VanArsdel Orders API", typeof(OrdersApp).Assembly, Policies.Configure);
        services.AddServiceDatabase<OrdersDbContext>(configuration, "Orders");
        services.AddVanArsdelCaching(configuration, "orders");
        services.AddEfOutbox<OrdersDbContext>();
        services.AddScoped<OrderService>();

        services.Configure<CarrierOptions>(configuration.GetSection("Carrier"));
        services.AddRefitClientFor<ICarrierApi>(configuration["Carrier:BaseUrl"]);
        services.AddScoped<CarrierService>();
        services.AddPspClient(configuration);
        services.AddScoped<PaymentService>();

        services.AddVanArsdelMessaging(configuration, "orders", x =>
        {
            x.AddConsumer<FulfilmentConsumers>();
            x.AddSagaStateMachine<OrderStateMachine, OrderState>()
                .EntityFrameworkRepository(r =>
                {
                    r.ConcurrencyMode = ConcurrencyMode.Optimistic;
                    r.ExistingDbContext<OrdersDbContext>();
                });
        });
        services.AddWebShopBus(configuration);
        services.AddHealthChecks().AddDbContextCheck<OrdersDbContext>("database");

        var app = builder.Build();
        app.UseVanArsdelApi(app.Environment);
        return app;
    }
}

public partial class Program
{
}

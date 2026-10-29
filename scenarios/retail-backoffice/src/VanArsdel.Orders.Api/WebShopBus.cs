using MassTransit;
using MassTransit.MultiBus;
using VanArsdel.BuildingBlocks.Messaging;
using VanArsdel.Orders.Api.Consumers;

namespace VanArsdel.Orders.Api;

/// <summary>
/// Second bus for the web shop platform. In production the web shop (hosted by the e-commerce partner)
/// writes to the Azure Service Bus queue "webshop-orders"; test environments use a RabbitMQ virtual host.
/// </summary>
public interface IWebShopBus : IBus
{
}



public static class WebShopBusRegistration
{
    public static IServiceCollection AddWebShopBus(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection("WebShopBus").Get<MessagingOptions>() ?? new MessagingOptions { Transport = "AzureServiceBus" };
        var queue = configuration["WebShopBus:QueueName"] ?? "webshop-orders";

        services.AddMassTransit<IWebShopBus>(x =>
        {
            x.AddConsumer<WebShopOrderConsumer>();
            void Endpoint(IBusRegistrationContext context, IReceiveConfigurator cfg) =>
                cfg.ReceiveEndpoint(queue, e => e.ConfigureConsumer<WebShopOrderConsumer>(context));

            switch (options.Transport.ToLowerInvariant())
            {
                case "inmemory":
                    x.UsingInMemory((context, cfg) => Endpoint(context, cfg));
                    break;
                case "rabbitmq":
                    x.UsingRabbitMq((context, cfg) =>
                    {
                        cfg.Host(options.Host, options.Port, options.VirtualHost, h =>
                        {
                            h.Username(options.Username ?? "guest");
                            h.Password(options.Password ?? "guest");
                        });
                        Endpoint(context, cfg);
                    });
                    break;
                default:
                    x.UsingAzureServiceBus((context, cfg) =>
                    {
                        cfg.Host(options.ConnectionString);
                        Endpoint(context, cfg);
                    });
                    break;
            }
        });
        return services;
    }
}

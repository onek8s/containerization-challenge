using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Humongous.Quoting.Messaging;

public static class BusRegistration
{
    /// <summary>
    /// Registers MassTransit with the transport from the <c>Bus</c> configuration section.
    /// Messages are published to fan-out exchanges named after the message types in
    /// Humongous.Quoting.Contracts.Messages; each application has its own queues.
    /// </summary>
    public static IServiceCollection AddQuotingBus(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        var options = configuration.GetSection(BusOptions.SectionName).Get<BusOptions>() ?? new BusOptions();
        services.AddSingleton(options);

        services.AddMassTransit(x =>
        {
            x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter(options.EndpointPrefix, false));
            configureConsumers?.Invoke(x);

            if (string.Equals(options.Transport, "InMemory", StringComparison.OrdinalIgnoreCase))
            {
                x.UsingInMemory((context, cfg) =>
                {
                    cfg.UseMessageRetry(r => r.Intervals(100, 500, 1000));
                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(options.Host, options.Port, options.VirtualHost, h =>
                    {
                        h.Username(options.Username);
                        h.Password(options.Password);
                        if (options.UseTls)
                            h.UseSsl(s => s.Protocol = System.Security.Authentication.SslProtocols.Tls12);
                    });
                    cfg.PrefetchCount = options.PrefetchCount;
                    cfg.UseDelayedRedelivery(r => r.Intervals(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)));
                    cfg.UseMessageRetry(r => r.Intervals(500, 2000, 5000));
                    cfg.ConfigureEndpoints(context);
                });
            }
        });

        return services;
    }
}

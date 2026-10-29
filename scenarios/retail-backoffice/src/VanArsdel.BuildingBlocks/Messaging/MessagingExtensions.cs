using System;
using System.Security.Authentication;
using GreenPipes;
using MassTransit;
using MassTransit.Definition;
using MassTransit.ExtensionsDependencyInjectionIntegration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace VanArsdel.BuildingBlocks.Messaging
{
    public class MessagingOptions
    {
        /// <summary>RabbitMq (production), AzureServiceBus or InMemory (single process, developer machines).</summary>
        public string Transport { get; set; } = "RabbitMq";
        public string Host { get; set; } = "mq01.corp.local";
        public ushort Port { get; set; } = 5672;
        public string VirtualHost { get; set; } = "retail";
        public string Username { get; set; }
        public string Password { get; set; }
        public bool UseSsl { get; set; }
        public string ConnectionString { get; set; }
        public int PrefetchCount { get; set; } = 16;
        public int[] RetryIntervalsMs { get; set; } = { 500, 2000, 10000 };
    }

    public static class MessagingExtensions
    {
        /// <summary>
        /// Registers MassTransit for a service. Queue names are prefixed with the service name so that
        /// two services can have a consumer for the same event without competing for it.
        /// </summary>
        public static IServiceCollection AddVanArsdelMessaging(this IServiceCollection services, IConfiguration configuration,
            string serviceName, Action<IServiceCollectionBusConfigurator> configureConsumers = null,
            Action<IBusRegistrationContext, IBusFactoryConfigurator> configureBus = null)
        {
            var options = configuration.GetSection("Messaging").Get<MessagingOptions>() ?? new MessagingOptions();

            services.AddMassTransit(x =>
            {
                x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter(serviceName, false));
                configureConsumers?.Invoke(x);
                ConfigureTransport(x, options, configureBus);
            });
            services.AddMassTransitHostedService(true);
            return services;
        }

        public static void ConfigureTransport<TConfigurator>(TConfigurator x, MessagingOptions options,
            Action<IBusRegistrationContext, IBusFactoryConfigurator> configureBus)
            where TConfigurator : IBusRegistrationConfigurator
        {
            switch ((options.Transport ?? "RabbitMq").ToLowerInvariant())
            {
                case "inmemory":
                    x.UsingInMemory((context, cfg) =>
                    {
                        cfg.UseMessageRetry(r => r.Intervals(options.RetryIntervalsMs));
                        configureBus?.Invoke(context, cfg);
                        cfg.ConfigureEndpoints(context);
                    });
                    break;

                case "azureservicebus":
                    x.UsingAzureServiceBus((context, cfg) =>
                    {
                        cfg.Host(options.ConnectionString);
                        cfg.UseMessageRetry(r => r.Intervals(options.RetryIntervalsMs));
                        configureBus?.Invoke(context, cfg);
                        cfg.ConfigureEndpoints(context);
                    });
                    break;

                default:
                    x.UsingRabbitMq((context, cfg) =>
                    {
                        cfg.Host(options.Host, options.Port, options.VirtualHost, h =>
                        {
                            h.Username(options.Username ?? "guest");
                            h.Password(options.Password ?? "guest");
                            if (options.UseSsl)
                            {
                                h.UseSsl(s =>
                                {
                                    s.Protocol = SslProtocols.Tls12;
                                    s.ServerName = options.Host;
                                });
                            }
                        });
                        cfg.PrefetchCount = options.PrefetchCount;
                        cfg.UseMessageRetry(r => r.Intervals(options.RetryIntervalsMs));
                        configureBus?.Invoke(context, cfg);
                        cfg.ConfigureEndpoints(context);
                    });
                    break;
            }
        }
    }
}

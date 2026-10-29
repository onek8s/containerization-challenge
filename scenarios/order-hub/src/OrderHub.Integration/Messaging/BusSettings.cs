using System;
using GreenPipes;
using Litware.OrderHub.Integration.Configuration;
using MassTransit;
using MassTransit.RabbitMqTransport;

namespace Litware.OrderHub.Integration.Messaging
{
    public class BusSettings
    {
        /// <summary>rabbitmq://host[:port]/vhost</summary>
        public Uri HostAddress { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public ushort PrefetchCount { get; set; } = 8;

        public static BusSettings Read(SettingsReader settings)
        {
            return new BusSettings
            {
                HostAddress = new Uri(settings.Require("RabbitMq.Host")),
                Username = settings.Require("RabbitMq.Username"),
                Password = settings.Require("RabbitMq.Password"),
                PrefetchCount = (ushort)settings.GetInt("RabbitMq.PrefetchCount", 8)
            };
        }
    }

    public static class HubBus
    {
        public static class Queues
        {
            public const string FulfilmentOrders = "orderhub.fulfilment.orders";
            public const string FulfilmentWarehouse = "orderhub.fulfilment.warehouse";
            public const string PartnersInvoicing = "orderhub.partners.invoicing";
            public const string PartnersDispatch = "orderhub.partners.dispatch";
            public const string ShopSyncShipments = "orderhub.shopsync.shipments";
            public const string ShopSyncRejections = "orderhub.shopsync.rejections";
        }

        public static IBusControl Create(BusSettings settings, Action<IRabbitMqBusFactoryConfigurator, IRabbitMqHost> configureEndpoints = null)
        {
            return Bus.Factory.CreateUsingRabbitMq(cfg =>
            {
                var host = cfg.Host(settings.HostAddress, h =>
                {
                    h.Username(settings.Username);
                    h.Password(settings.Password);
                    h.Heartbeat(30);
                });

                cfg.PrefetchCount = settings.PrefetchCount;
                configureEndpoints?.Invoke(cfg, host);
            });
        }

        /// <summary>Retry settings shared by all consumer endpoints; after the last attempt the message goes to the _error queue.</summary>
        public static void UseHubRetry(this IReceiveEndpointConfigurator endpoint)
        {
            endpoint.UseMessageRetry(r => r.Intervals(
                TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2)));
        }
    }
}

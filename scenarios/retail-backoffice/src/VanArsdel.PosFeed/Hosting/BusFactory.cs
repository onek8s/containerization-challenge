using System;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MassTransit.RabbitMqTransport;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VanArsdel.PosFeed.Hosting
{
    public class BusOptions
    {
        public string Transport { get; set; } = "RabbitMq";
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string VirtualHost { get; set; } = "/";
        public string Username { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public bool UseSsl { get; set; }
    }

    public static class BusFactory
    {
        /// <summary>MassTransit 5 style: the bus is built by hand and started by a hosted service.</summary>
        public static IBusControl Create(BusOptions options)
        {
            if (string.Equals(options.Transport, "InMemory", StringComparison.OrdinalIgnoreCase))
            {
                return Bus.Factory.CreateUsingInMemory(cfg => { });
            }

            return Bus.Factory.CreateUsingRabbitMq(cfg =>
            {
                var vhost = string.IsNullOrEmpty(options.VirtualHost) || options.VirtualHost == "/" ? string.Empty : options.VirtualHost.TrimStart('/');
                cfg.Host(new Uri($"rabbitmq://{options.Host}:{options.Port}/{vhost}"), h =>
                {
                    h.Username(options.Username);
                    h.Password(options.Password);
                    if (options.UseSsl)
                    {
                        h.UseSsl(s =>
                        {
                            s.Protocol = SslProtocols.Tls12;
                            s.ServerName = options.Host;
                        });
                    }
                });
            });
        }
    }

    public class BusService : IHostedService
    {
        private readonly IBusControl _bus;
        private readonly ILogger<BusService> _logger;

        public BusService(IBusControl bus, ILogger<BusService> logger)
        {
            _bus = bus;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _bus.StartAsync(cancellationToken);
            _logger.LogInformation("Bus started at {Address}", _bus.Address);
        }

        public Task StopAsync(CancellationToken cancellationToken) => _bus.StopAsync(cancellationToken);
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Proseware.ControlTower.Integration.Messaging;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddControlTowerMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MessagingOptions>(configuration.GetSection(MessagingOptions.Section));
        var transport = configuration[$"{MessagingOptions.Section}:Transport"] ?? "RabbitMq";
        if (string.Equals(transport, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<InMemoryEventBus>();
            services.AddSingleton<IEventBus>(sp => sp.GetRequiredService<InMemoryEventBus>());
        }
        else
        {
            var connectionString = configuration.GetConnectionString("RabbitMq")
                ?? throw new InvalidOperationException("Connection string 'RabbitMq' is missing.");
            services.AddSingleton<IEventBus>(sp => new RabbitEventBus(
                connectionString,
                sp.GetRequiredService<IOptions<MessagingOptions>>(),
                sp.GetRequiredService<ILogger<RabbitEventBus>>()));
        }
        return services;
    }
}

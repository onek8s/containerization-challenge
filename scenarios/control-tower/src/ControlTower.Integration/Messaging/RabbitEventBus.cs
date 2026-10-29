using EasyNetQ;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proseware.ControlTower.Domain.Events;

namespace Proseware.ControlTower.Integration.Messaging;

public sealed class RabbitEventBus : IEventBus, IDisposable
{
    private readonly IBus _bus;
    private readonly MessagingOptions _options;
    private readonly ILogger<RabbitEventBus> _logger;

    public RabbitEventBus(string connectionString, IOptions<MessagingOptions> options, ILogger<RabbitEventBus> logger)
    {
        _options = options.Value;
        _logger = logger;
        _bus = RabbitHutch.CreateBus(connectionString, register => register.EnableNewtonsoftJson());
    }

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class, IIntegrationEvent =>
        _bus.PubSub.PublishAsync(message, cancellationToken);

    public async Task<IDisposable> SubscribeAsync<T>(string subscriptionId, Func<T, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
        where T : class, IIntegrationEvent
    {
        var queue = $"{_options.Environment}.{subscriptionId}";
        var result = await _bus.PubSub.SubscribeAsync<T>(
            queue,
            async (message, ct) =>
            {
                try
                {
                    await handler(message, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Handler {Subscription} failed for {MessageType} {EventId}", queue, typeof(T).Name, message.EventId);
                    throw;
                }
            },
            c => c.WithPrefetchCount(_options.PrefetchCount),
            cancellationToken);
        _logger.LogInformation("Subscribed {Subscription} to {MessageType}", queue, typeof(T).Name);
        return result;
    }

    public void Dispose() => _bus.Dispose();
}

using Proseware.ControlTower.Domain.Events;

namespace Proseware.ControlTower.Integration.Messaging;

/// <summary>Publish/subscribe between the Control Tower applications.</summary>
public interface IEventBus
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class, IIntegrationEvent;

    /// <summary>
    /// Subscribes a handler. All subscribers that use the same <paramref name="subscriptionId"/> share one queue,
    /// so each message is handled by one of them.
    /// </summary>
    Task<IDisposable> SubscribeAsync<T>(string subscriptionId, Func<T, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
        where T : class, IIntegrationEvent;
}

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Proseware.ControlTower.Domain.Events;

namespace Proseware.ControlTower.Integration.Messaging;

/// <summary>In-process bus for development and tests. Same semantics as the broker: one handler per subscription id.</summary>
public sealed class InMemoryEventBus : IEventBus
{
    private readonly ConcurrentDictionary<(Type, string), List<Func<object, CancellationToken, Task>>> _subscriptions = new();
    private readonly ILogger<InMemoryEventBus>? _logger;
    private int _roundRobin;

    public InMemoryEventBus(ILogger<InMemoryEventBus>? logger = null) => _logger = logger;

    public List<IIntegrationEvent> Published { get; } = new();

    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class, IIntegrationEvent
    {
        lock (Published) Published.Add(message);
        foreach (var ((type, _), handlers) in _subscriptions.ToArray())
        {
            if (!type.IsAssignableFrom(typeof(T))) continue;
            Func<object, CancellationToken, Task> handler;
            lock (handlers)
            {
                if (handlers.Count == 0) continue;
                handler = handlers[Interlocked.Increment(ref _roundRobin) % handlers.Count];
            }
            try
            {
                await handler(message, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "In-memory handler failed for {MessageType}", typeof(T).Name);
            }
        }
    }

    public Task<IDisposable> SubscribeAsync<T>(string subscriptionId, Func<T, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
        where T : class, IIntegrationEvent
    {
        var list = _subscriptions.GetOrAdd((typeof(T), subscriptionId), _ => new List<Func<object, CancellationToken, Task>>());
        Func<object, CancellationToken, Task> wrapped = (m, ct) => handler((T)m, ct);
        lock (list) list.Add(wrapped);
        return Task.FromResult<IDisposable>(new Unsubscriber(() => { lock (list) list.Remove(wrapped); }));
    }

    private sealed class Unsubscriber : IDisposable
    {
        private Action? _action;

        public Unsubscriber(Action action) => _action = action;

        public void Dispose() => Interlocked.Exchange(ref _action, null)?.Invoke();
    }
}

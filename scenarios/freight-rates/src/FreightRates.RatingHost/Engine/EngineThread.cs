using System.Collections.Concurrent;

namespace FreightRates.RatingHost.Engine;

/// <summary>
/// Runs all engine calls on one dedicated thread. RateEngine sessions are bound to the
/// thread that opened them and must not be called concurrently.
/// </summary>
public sealed class EngineThread : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;

    public EngineThread(string name)
    {
        _thread = new Thread(Run) { Name = name, IsBackground = true };
        _thread.Start();
    }

    public int QueueLength => _queue.Count;

    public Task<T> InvokeAsync<T>(Func<T> work, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        _queue.Add(() =>
        {
            registration.Dispose();
            if (tcs.Task.IsCompleted)
                return;
            try
            {
                tcs.TrySetResult(work());
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    public Task InvokeAsync(Action work, CancellationToken cancellationToken = default) =>
        InvokeAsync(() => { work(); return true; }, cancellationToken);

    private void Run()
    {
        foreach (var work in _queue.GetConsumingEnumerable())
            work();
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(10));
        _queue.Dispose();
    }
}

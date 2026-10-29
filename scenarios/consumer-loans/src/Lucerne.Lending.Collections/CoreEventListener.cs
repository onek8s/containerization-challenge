using Lucerne.Lending.CoreBanking;
using Lucerne.Lending.Workflow;

namespace Lucerne.Lending.Collections;

/// <summary>Reads loan events from the core (end of day, payment posting) and keeps collections up to date.</summary>
public sealed class CoreEventListener : BackgroundService
{
    private readonly ICoreEventSource _events;
    private readonly CollectionsService _collections;
    private readonly ILogger<CoreEventListener> _log;

    public CoreEventListener(ICoreEventSource events, CollectionsService collections, ILogger<CoreEventListener> log)
    {
        _events = events;
        _collections = collections;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var failures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var evt = await _events.ReceiveAsync(TimeSpan.FromSeconds(30), stoppingToken);
                if (evt == null) continue;
                await _collections.HandleEventAsync(evt, stoppingToken);
                failures = 0;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures++;
                _log.LogError(ex, "Core event processing failed ({Failures} in a row)", failures);
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(300, 5 * failures)), stoppingToken);
            }
        }
    }
}

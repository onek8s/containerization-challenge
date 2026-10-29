using Litware.DocControl.Platform;
using Microsoft.Extensions.Options;

namespace Litware.DocControl.Indexer;

public sealed class IndexerWorker : BackgroundService
{
    public const string MutexName = @"Global\LitwareDocIndexer";

    private readonly DropScanner _scanner;
    private readonly IndexerOptions _options;
    private readonly EventLogWriter _eventLog;
    private readonly ILogger<IndexerWorker> _log;

    public IndexerWorker(DropScanner scanner, IOptions<IndexerOptions> options, EventLogWriter eventLog, ILogger<IndexerWorker> log)
    {
        _scanner = scanner;
        _options = options.Value;
        _eventLog = eventLog;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Only one indexer may work on the drop share of this server (a manual --scan-once included).
        using var mutex = new Mutex(false, MutexName, out _);
        await Task.Yield();

        _eventLog.Info(2000, "Indexer started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            var owned = false;
            try
            {
                owned = mutex.WaitOne(TimeSpan.Zero);
                // Synchronous on purpose: the mutex must be released by the thread that acquired it.
                if (owned) _scanner.ScanAsync(stoppingToken).GetAwaiter().GetResult();
                else _log.LogInformation("Another indexer is scanning, waiting for the next round");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Drop scan failed");
                _eventLog.Error(2900, "Drop scan failed: " + ex);
            }
            finally
            {
                if (owned) mutex.ReleaseMutex();
            }

            try { await Task.Delay(TimeSpan.FromSeconds(_options.ScanSeconds), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}

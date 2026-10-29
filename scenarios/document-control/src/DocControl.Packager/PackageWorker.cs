using Litware.DocControl.Data;
using Litware.DocControl.Platform;
using Microsoft.Extensions.Options;

namespace Litware.DocControl.Packager;

public sealed class PackageWorker : BackgroundService
{
    private readonly TransmittalRepository _transmittals;
    private readonly PackageBuilder _builder;
    private readonly TransmittalMailer _mailer;
    private readonly EventLogWriter _eventLog;
    private readonly PackagerOptions _options;
    private readonly ILogger<PackageWorker> _log;
    private readonly string _workerName = $"{Environment.MachineName}:{Environment.ProcessId}";

    public PackageWorker(TransmittalRepository transmittals, PackageBuilder builder, TransmittalMailer mailer, EventLogWriter eventLog,
        IOptions<PackagerOptions> options, ILogger<PackageWorker> log)
    {
        _transmittals = transmittals;
        _builder = builder;
        _mailer = mailer;
        _eventLog = eventLog;
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var parallel = _options.MaxParallelBuilds > 0
            ? _options.MaxParallelBuilds
            : Math.Max(1, HostInfo.Current.LogicalProcessors / 2);

        _log.LogInformation("Packager started as {Worker}, {Parallel} parallel builds, work folder {Work}", _workerName, parallel, _builder.WorkFolder);
        _eventLog.Info(1000, $"Packager started, {parallel} parallel builds.");

        using var slots = new SemaphoreSlim(parallel);
        var running = new List<Task>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                running.RemoveAll(t => t.IsCompleted);
                var space = _builder.CheckFreeSpace();
                if (space is not null)
                {
                    _log.LogWarning("Not starting builds: {Problem}", space);
                    _eventLog.Warning(1100, space);
                }
                else
                {
                    while (await slots.WaitAsync(0, stoppingToken))
                    {
                        var t = await _transmittals.ClaimNextAsync(_workerName, DateTime.UtcNow.AddMinutes(-_options.StaleMinutes), stoppingToken);
                        if (t is null)
                        {
                            slots.Release();
                            break;
                        }
                        running.Add(Task.Run(async () =>
                        {
                            try { await ProcessAsync(t, stoppingToken); }
                            finally { slots.Release(); }
                        }, CancellationToken.None));
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Polling for transmittals failed");
                _eventLog.Error(1900, "Polling for transmittals failed: " + ex);
            }

            try { await Task.Delay(TimeSpan.FromSeconds(_options.PollSeconds), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        await Task.WhenAll(running);
    }

    private async Task ProcessAsync(TransmittalRecord t, CancellationToken stoppingToken)
    {
        try
        {
            var package = await _builder.BuildAsync(t, stoppingToken);
            await _transmittals.MarkIssuedAsync(t.Id, package.RelativePath, package.Size);
            _eventLog.Info(1200, $"{t.Number} issued: {package.Files.Count} documents, {package.Size} bytes.");

            try
            {
                _mailer.SendIssued(t, package);
            }
            catch (Exception ex)
            {
                // The package is published; the controller can send it manually.
                _log.LogError(ex, "{Transmittal}: mail to {Recipient} failed", t.Number, t.RecipientEmail);
                _eventLog.Warning(1210, $"{t.Number} issued but the mail to {t.RecipientEmail} failed: {ex.Message}");
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await _transmittals.MarkFailedAsync(t.Id, "Service stopped during the build.", retry: true);
        }
        catch (Exception ex)
        {
            var retry = t.Attempts < _options.MaxAttempts;
            _log.LogError(ex, "{Transmittal}: build failed (attempt {Attempt})", t.Number, t.Attempts);
            await _transmittals.MarkFailedAsync(t.Id, ex.Message, retry);
            _eventLog.Error(1300, $"{t.Number} failed (attempt {t.Attempts}): {ex}");
            if (!retry)
            {
                try { _mailer.SendFailed(t, ex.Message); }
                catch (Exception mailEx) { _log.LogError(mailEx, "Failure mail for {Transmittal} could not be sent", t.Number); }
            }
        }
    }
}

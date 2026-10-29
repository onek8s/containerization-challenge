using Litware.DocControl.Data.Storage;
using Litware.DocControl.Platform;
using Microsoft.Extensions.Options;

namespace Litware.DocControl.Indexer;

public sealed record ScanResult(int Accepted, int Rejected, int Skipped, TimeSpan Elapsed);

public sealed class DropScanner
{
    private readonly DropFileProcessor _processor;
    private readonly StorageOptions _storage;
    private readonly IndexerOptions _options;
    private readonly EventLogWriter _eventLog;
    private readonly ILogger<DropScanner> _log;

    public DropScanner(DropFileProcessor processor, IOptions<StorageOptions> storage, IOptions<IndexerOptions> options, EventLogWriter eventLog,
        ILogger<DropScanner> log)
    {
        _processor = processor;
        _storage = storage.Value;
        _options = options.Value;
        _eventLog = eventLog;
        _log = log;
    }

    public async Task<ScanResult> ScanAsync(CancellationToken ct)
    {
        var timer = PerfTimer.Start();
        int accepted = 0, rejected = 0, skipped = 0;
        var cutoff = DateTime.UtcNow.AddSeconds(-_options.MinFileAgeSeconds);

        foreach (var projectFolder in Directory.EnumerateDirectories(_storage.DropRoot))
        {
            if (Path.GetFileName(projectFolder).StartsWith('_')) continue;

            foreach (var file in DirectoryScanner.EnumerateFiles(projectFolder))
            {
                ct.ThrowIfCancellationRequested();
                if (IsIgnored(file)) continue;
                if (file.LastWriteUtc > cutoff) { skipped++; continue; }

                try
                {
                    switch (await _processor.ProcessAsync(projectFolder, file, ct))
                    {
                        case DropOutcome.Accepted: accepted++; break;
                        case DropOutcome.Rejected: rejected++; break;
                        default: skipped++; break;
                    }
                }
                catch (IOException ex)
                {
                    // Typically still locked by the user's copy; try again on the next scan.
                    skipped++;
                    _log.LogInformation("{File} skipped: {Message}", file.Name, ex.Message);
                }
            }
        }

        var result = new ScanResult(accepted, rejected, skipped, timer.Elapsed);
        if (accepted + rejected > 0)
        {
            _log.LogInformation("Scan finished in {Elapsed:0.00} s: {Accepted} accepted, {Rejected} rejected, {Skipped} skipped",
                result.Elapsed.TotalSeconds, accepted, rejected, skipped);
            _eventLog.Info(2100, $"Drop scan: {accepted} accepted, {rejected} rejected, {skipped} skipped.");
        }
        return result;
    }

    private static bool IsIgnored(ScannedFile file) =>
        (file.Attributes & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.Temporary)) != 0
        || file.Name.StartsWith("~$", StringComparison.Ordinal)
        || file.Name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
        || file.Name.EndsWith(".reason.txt", StringComparison.OrdinalIgnoreCase)
        || file.Name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase);
}

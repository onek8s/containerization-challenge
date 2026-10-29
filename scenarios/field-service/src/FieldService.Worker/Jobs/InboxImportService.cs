using System.Text;
using FieldService.Data;
using FieldService.Data.Entities;
using FieldService.Data.Services;
using Microsoft.Extensions.Options;

namespace FieldService.Worker.Jobs;

/// <summary>
/// Picks up job request files (*.csv) dropped by customers into the inbox share,
/// creates work orders and moves the file to the archive or error folder.
/// </summary>
public class InboxImportService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly WorkerOptions _options;
    private readonly ILogger<InboxImportService> _log;

    public InboxImportService(IServiceScopeFactory scopes, IOptions<WorkerOptions> options, ILogger<InboxImportService> log)
    {
        _scopes = scopes;
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Watching {Inbox} every {Seconds}s", _options.InboxFolder, _options.InboxPollSeconds);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, _options.InboxPollSeconds)));
        do
        {
            try
            {
                await ProcessInboxAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Inbox run failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> ProcessInboxAsync(CancellationToken ct)
    {
        if (!Directory.Exists(_options.InboxFolder))
        {
            _log.LogWarning("Inbox folder {Inbox} is not reachable", _options.InboxFolder);
            return 0;
        }

        using var folderLock = FolderLock.TryAcquire(_options.StateFolder, "inbox");
        if (folderLock == null)
        {
            _log.LogInformation("Inbox is being processed by another instance");
            return 0;
        }

        var processed = 0;
        foreach (var file in Directory.EnumerateFiles(_options.InboxFolder, "*.csv").OrderBy(f => File.GetLastWriteTimeUtc(f)))
        {
            ct.ThrowIfCancellationRequested();

            // Skip files that are still being copied in.
            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) < TimeSpan.FromSeconds(5))
            {
                continue;
            }

            await ImportFileAsync(file, ct);
            processed++;
        }

        return processed;
    }

    private async Task ImportFileAsync(string path, CancellationToken ct)
    {
        var fileName = Path.GetFileName(path);
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FieldServiceContext>();
        var service = scope.ServiceProvider.GetRequiredService<WorkOrderService>();

        var record = new ImportedFile { FileName = fileName, ProcessedUtc = DateTime.UtcNow };
        var errors = new List<string>();
        try
        {
            WorkRequestParseResult parsed;
            using (var reader = new StreamReader(path, Encoding.UTF8))
            {
                parsed = WorkRequestCsv.Parse(reader);
            }

            errors.AddRange(parsed.Errors);
            foreach (var request in parsed.Requests)
            {
                try
                {
                    var order = await service.CreateAsync(request, ct);
                    record.OrdersCreated++;
                    _log.LogInformation("Created {Number} from {File} ({Reference})", order.Number, fileName, request.CustomerReference);
                }
                catch (InvalidOperationException ex)
                {
                    errors.Add(ex.Message);
                }
            }
        }
        catch (IOException ex)
        {
            _log.LogWarning(ex, "Could not read {File}, will retry", fileName);
            return;
        }

        record.RowsRejected = errors.Count;
        record.Error = errors.Count > 0 ? string.Join(Environment.NewLine, errors) : null;
        db.ImportedFiles.Add(record);
        await db.SaveChangesAsync(ct);

        var target = record.OrdersCreated == 0 && errors.Count > 0 ? _options.ErrorFolder : _options.ArchiveFolder;
        Directory.CreateDirectory(target);
        var stamped = $"{Path.GetFileNameWithoutExtension(fileName)}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(fileName)}";
        File.Move(path, Path.Combine(target, stamped));
        if (errors.Count > 0)
        {
            await File.WriteAllLinesAsync(Path.Combine(target, stamped + ".errors.txt"), errors, ct);
            _log.LogWarning("{File}: {Created} orders created, {Rejected} rows rejected", fileName, record.OrdersCreated, errors.Count);
        }
    }
}

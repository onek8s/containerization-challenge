using System.Globalization;
using System.Text;
using FieldService.Data;
using FieldService.Data.Entities;
using FieldService.Data.Services;
using Microsoft.Extensions.Options;

namespace FieldService.Worker.Jobs;

/// <summary>
/// Once a day: exports completed work orders to the finance outbox and writes the SLA breach report.
/// The date of the last successful run is kept in a state file so a restart does not export twice.
/// </summary>
public class NightlyJobsService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly WorkerOptions _options;
    private readonly ILogger<NightlyJobsService> _log;
    private readonly TimeZoneInfo _zone;

    public NightlyJobsService(IServiceScopeFactory scopes, IOptions<WorkerOptions> options, ILogger<NightlyJobsService> log)
    {
        _scopes = scopes;
        _options = options.Value;
        _log = log;
        _zone = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZone);
    }

    private string StateFile => Path.Combine(_options.StateFolder, "nightly.last");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Nightly jobs scheduled at {Time} ({Zone})", _options.NightlyRunAt, _zone.Id);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _zone);
                if (local.TimeOfDay >= _options.NightlyRunAt && ReadLastRun() < local.Date)
                {
                    await RunAsync(local.Date, stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Nightly run failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunAsync(DateTime businessDate, CancellationToken ct)
    {
        using var folderLock = FolderLock.TryAcquire(_options.StateFolder, "nightly");
        if (folderLock == null)
        {
            _log.LogInformation("Nightly jobs are running in another instance");
            return;
        }

        await ExportInvoicesAsync(ct);
        await WriteSlaReportAsync(businessDate, ct);

        Directory.CreateDirectory(_options.StateFolder);
        await File.WriteAllTextAsync(StateFile, businessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ct);
    }

    private DateTime ReadLastRun()
    {
        if (!File.Exists(StateFile))
        {
            return DateTime.MinValue;
        }

        return DateTime.TryParseExact(File.ReadAllText(StateFile).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : DateTime.MinValue;
    }

    private async Task ExportInvoicesAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FieldServiceContext>();
        var service = scope.ServiceProvider.GetRequiredService<WorkOrderService>();
        var numbers = scope.ServiceProvider.GetRequiredService<NumberSequence>();

        var orders = await service.GetUninvoicedAsync(ct);
        if (orders.Count == 0)
        {
            _log.LogInformation("No completed work orders to invoice");
            return;
        }

        var batchNumber = await numbers.NextAsync("Invoice", "INV", 4, DateTime.UtcNow, ct);
        var fileName = $"FS_{batchNumber}.csv";
        Directory.CreateDirectory(_options.OutboxFolder);
        var tempPath = Path.Combine(_options.OutboxFolder, fileName + ".tmp");

        decimal total;
        await using (var writer = new StreamWriter(tempPath, false, new UTF8Encoding(false)))
        {
            total = InvoiceExport.Write(writer, batchNumber, orders);
        }

        foreach (var order in orders)
        {
            order.InvoiceBatch = batchNumber;
            order.Status = WorkOrderStatus.Invoiced;
        }

        db.InvoiceBatches.Add(new InvoiceBatch
        {
            BatchNumber = batchNumber,
            CreatedUtc = DateTime.UtcNow,
            FileName = fileName,
            OrderCount = orders.Count,
            TotalAmount = total
        });
        await db.SaveChangesAsync(ct);

        File.Move(tempPath, Path.Combine(_options.OutboxFolder, fileName), overwrite: true);
        _log.LogInformation("Invoice batch {Batch}: {Count} orders, total {Total}", batchNumber, orders.Count, total);
    }

    private async Task WriteSlaReportAsync(DateTime businessDate, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<WorkOrderService>();
        var overdue = await service.GetOverdueAsync(ct);

        Directory.CreateDirectory(_options.ReportFolder);
        var path = Path.Combine(_options.ReportFolder, $"sla-breaches-{businessDate:yyyy-MM-dd}.txt");
        var sb = new StringBuilder();
        sb.AppendLine($"SLA breaches as of {TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _zone):yyyy-MM-dd HH:mm}");
        sb.AppendLine(new string('-', 72));
        foreach (var o in overdue)
        {
            var late = DateTime.UtcNow - o.DueUtc;
            sb.AppendLine($"{o.Number,-15} {o.Customer?.AccountCode,-10} {o.Priority,-9} {o.Status,-10} late {late.TotalHours,6:0.0} h  {o.Technician?.Name ?? "(unassigned)"}");
        }

        sb.AppendLine($"Total: {overdue.Count}");
        await File.WriteAllTextAsync(path, sb.ToString(), ct);
        _log.LogInformation("SLA report written to {Path} ({Count} breaches)", path, overdue.Count);
    }
}

using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Documents;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Jobs.Infrastructure;

namespace Proseware.ControlTower.Jobs.Jobs;

/// <summary>Removes documents past their retention date.</summary>
public sealed class DocumentPurgeJob
{
    private readonly ControlTowerDbContext _db;
    private readonly IDocumentStore _store;
    private readonly IClock _clock;
    private readonly ILogger<DocumentPurgeJob> _logger;

    public DocumentPurgeJob(ControlTowerDbContext db, IDocumentStore store, IClock clock, ILogger<DocumentPurgeJob> logger)
    {
        _db = db;
        _store = store;
        _clock = clock;
        _logger = logger;
    }

    [DisableConcurrentExecution(3600)]
    public async Task RunAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var expired = await _db.Documents.AsNoTracking()
            .Where(d => d.DeleteAfter != null && d.DeleteAfter < now)
            .OrderBy(d => d.DeleteAfter)
            .Select(d => d.Id)
            .Take(5000)
            .ToListAsync(ct);
        foreach (var id in expired)
        {
            ct.ThrowIfCancellationRequested();
            await _store.DeleteAsync(id, ct);
        }
        _logger.LogInformation("Document purge removed {Count} documents", expired.Count);
    }
}

/// <summary>Deletes raw vehicle positions older than the retention period in small batches.</summary>
public sealed class PositionRetentionJob
{
    private readonly ControlTowerDbContext _db;
    private readonly IClock _clock;
    private readonly JobsOptions _options;
    private readonly ILogger<PositionRetentionJob> _logger;

    public PositionRetentionJob(ControlTowerDbContext db, IClock clock, IOptions<JobsOptions> options, ILogger<PositionRetentionJob> logger)
    {
        _db = db;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    [DisableConcurrentExecution(3600)]
    public async Task RunAsync(CancellationToken ct)
    {
        var limit = _clock.UtcNow.AddDays(-_options.PositionRetentionDays);
        var total = 0;
        while (!ct.IsCancellationRequested)
        {
            int deleted;
            if (_db.IsSqlite)
            {
                var ids = await _db.VehiclePositions.Where(p => p.RecordedAt < limit).Select(p => p.Id).Take(5000).ToListAsync(ct);
                _db.VehiclePositions.RemoveRange(ids.Select(id => new Domain.Fleet.VehiclePosition { Id = id }));
                deleted = await _db.SaveChangesAsync(ct);
            }
            else
            {
                // Batches keep the transaction log and lock escalation under control on the large table.
                deleted = await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE TOP (5000) FROM ct.VehiclePosition WHERE RecordedAt < {limit}", ct);
            }
            total += deleted;
            if (deleted < 5000) break;
        }
        _logger.LogInformation("Position retention removed {Count} rows older than {Limit:yyyy-MM-dd}", total, limit);
    }
}

/// <summary>
/// Imports the fuel surcharge percentages published by the carriers. The purchasing team drops a CSV
/// (carrier;mode;percent;valid_from) in the import folder; processed files move to "done".
/// </summary>
public sealed class FuelSurchargeImportJob
{
    private readonly ControlTowerDbContext _db;
    private readonly JobsOptions _options;
    private readonly ILogger<FuelSurchargeImportJob> _logger;

    public FuelSurchargeImportJob(ControlTowerDbContext db, IOptions<JobsOptions> options, ILogger<FuelSurchargeImportJob> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    [DisableConcurrentExecution(600)]
    public async Task RunAsync(CancellationToken ct)
    {
        if (!Directory.Exists(_options.FuelImportFolder)) return;
        foreach (var file in Directory.GetFiles(_options.FuelImportFolder, "*.csv").OrderBy(f => f))
        {
            try
            {
                var updated = await ImportAsync(file, ct);
                Move(file, "done");
                _logger.LogInformation("Fuel surcharges from {File}: {Count} rate cards updated", Path.GetFileName(file), updated);
            }
            catch (Exception ex) when (ex is CsvHelperException or FormatException or InvalidOperationException)
            {
                _logger.LogError(ex, "Fuel surcharge file {File} rejected", file);
                Move(file, "error");
            }
        }
    }

    private async Task<int> ImportAsync(string file, CancellationToken ct)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = ";", HasHeaderRecord = true, PrepareHeaderForMatch = a => a.Header.Trim().ToLowerInvariant() };
        using var reader = new StreamReader(file);
        using var csv = new CsvReader(reader, config);
        var rows = csv.GetRecords<FuelRow>().ToList();

        var carriers = await _db.Carriers.ToDictionaryAsync(c => c.Code, StringComparer.OrdinalIgnoreCase, ct);
        var cards = await _db.RateCards.ToListAsync(ct);
        var updated = 0;
        foreach (var row in rows)
        {
            if (!carriers.TryGetValue(row.Carrier.Trim(), out var carrier))
                throw new InvalidOperationException($"Unknown carrier '{row.Carrier}'.");
            if (row.Percent is < 0 or > 60)
                throw new InvalidOperationException($"Fuel surcharge {row.Percent} % for {row.Carrier} is out of range.");
            var mode = Enum.Parse<TransportMode>(row.Mode, ignoreCase: true);
            foreach (var card in cards.Where(c => c.CarrierId == carrier.Id && c.Mode == mode && c.ValidTo >= row.Valid_From))
            {
                card.FuelSurchargePercent = row.Percent;
                updated++;
            }
        }
        await _db.SaveChangesAsync(ct);
        return updated;
    }

    private static void Move(string file, string folder)
    {
        var target = Path.Combine(Path.GetDirectoryName(file)!, folder);
        Directory.CreateDirectory(target);
        File.Move(file, Path.Combine(target, $"{DateTime.Now:yyyyMMddHHmmss}_{Path.GetFileName(file)}"), overwrite: true);
    }

    private sealed class FuelRow
    {
        public string Carrier { get; set; } = "";
        public string Mode { get; set; } = "";
        public decimal Percent { get; set; }
        public DateTime Valid_From { get; set; }
    }
}

using FreightRates.RatingHost.Engine;
using Microsoft.Extensions.Options;

namespace FreightRates.RatingHost.Services;

/// <summary>
/// Installs tariff files dropped in the incoming folder (shared as MRSData$\Incoming for the
/// admin portal) into the engine data path and reloads the engine. A tariff set is installed
/// when its release.ok marker file arrives.
/// </summary>
public sealed class TariffImportService : BackgroundService
{
    private const string ReleaseMarker = "release.ok";
    private static readonly string[] TariffExtensions = { ".mrt", ".mrx", ".mrz" };

    private readonly EngineOptions _options;
    private readonly IHostedRatingEngine _engine;
    private readonly ILogger<TariffImportService> _logger;
    private readonly SemaphoreSlim _signal = new(0);

    public TariffImportService(IOptions<EngineOptions> options, IHostedRatingEngine engine, ILogger<TariffImportService> logger)
    {
        _options = options.Value;
        _engine = engine;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Directory.Exists(_options.IncomingPath))
        {
            _logger.LogWarning("Tariff incoming folder {Path} does not exist; tariff import disabled", _options.IncomingPath);
            return;
        }

        using var watcher = new FileSystemWatcher(_options.IncomingPath, ReleaseMarker)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        };
        watcher.Created += (_, _) => _signal.Release();
        watcher.Changed += (_, _) => _signal.Release();

        while (!stoppingToken.IsCancellationRequested)
        {
            if (File.Exists(Path.Combine(_options.IncomingPath, ReleaseMarker)))
            {
                try
                {
                    await InstallAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Tariff import failed");
                }
            }

            // Wake on the watcher or poll every ten minutes in case an event was missed.
            await _signal.WaitAsync(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }

    private async Task InstallAsync(CancellationToken cancellationToken)
    {
        // Give the copy from the admin portal a moment to finish.
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

        var files = Directory.EnumerateFiles(_options.IncomingPath)
            .Where(f => TariffExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToList();
        var marker = Path.Combine(_options.IncomingPath, ReleaseMarker);

        if (files.Count == 0)
        {
            _logger.LogWarning("Release marker found but no tariff files in {Path}", _options.IncomingPath);
            File.Delete(marker);
            return;
        }

        var archive = Path.Combine(_options.ArchivePath, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(archive);

        foreach (var file in files)
        {
            var target = Path.Combine(_options.DataPath, Path.GetFileName(file));
            if (File.Exists(target))
                File.Copy(target, Path.Combine(archive, Path.GetFileName(file)), overwrite: true);
            File.Copy(file, target, overwrite: true);
            File.Delete(file);
            _logger.LogInformation("Installed tariff file {File}", Path.GetFileName(file));
        }
        File.Delete(marker);

        var info = await _engine.ReloadTariffsAsync(cancellationToken);
        _logger.LogInformation("Tariff {Tariff} effective {Effective} active after import of {Count} files",
            info.TariffName, info.EffectiveDate, files.Count);
    }
}

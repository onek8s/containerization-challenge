using FurnaceRecords.Acquisition;
using FurnaceRecords.Acquisition.Simulation;

namespace FurnaceRecords.Daq;

/// <summary>Runs one acquisition thread per configured furnace.</summary>
internal sealed class AcquisitionService : BackgroundService
{
    private readonly AcquisitionOptions _options;
    private readonly IAcquisitionFactory _factory;
    private readonly IServiceScopeFactory _scopes;
    private readonly LiveReadings _live;
    private readonly SimulatedPlant _plant;
    private readonly ILoggerFactory _loggers;

    public AcquisitionService(AcquisitionOptions options, IAcquisitionFactory factory, IServiceScopeFactory scopes,
        LiveReadings live, SimulatedPlant plant, ILoggerFactory loggers)
    {
        _options = options;
        _factory = factory;
        _scopes = scopes;
        _live = live;
        _plant = plant;
        _loggers = loggers;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var simulator = string.Equals(_options.Provider, "Simulator", StringComparison.OrdinalIgnoreCase);
        var threads = _options.Furnaces.Select(f =>
        {
            var worker = new FurnaceWorker(f, _options, _factory, _scopes, _live, simulator ? _plant : null,
                _loggers.CreateLogger($"FurnaceRecords.Daq.Furnace.{f.Code}"));
            var thread = new Thread(() => worker.Run(stoppingToken)) { Name = $"Furnace {f.Code}", IsBackground = true };
            thread.Start();
            return thread;
        }).ToList();

        return Task.Run(() =>
        {
            stoppingToken.WaitHandle.WaitOne();
            foreach (var t in threads) t.Join(TimeSpan.FromSeconds(10));
        }, CancellationToken.None);
    }
}

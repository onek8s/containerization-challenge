using FurnaceRecords.Acquisition.Board;
using FurnaceRecords.Acquisition.Opc;
using FurnaceRecords.Acquisition.Simulation;
using Microsoft.Extensions.Logging;

namespace FurnaceRecords.Acquisition;

public sealed class AcquisitionFactory : IAcquisitionFactory
{
    private readonly AcquisitionOptions _options;
    private readonly SimulatedPlant _plant;
    private readonly TimeProvider _time;
    private readonly ILoggerFactory _loggers;

    public AcquisitionFactory(AcquisitionOptions options, SimulatedPlant plant, TimeProvider time, ILoggerFactory loggers)
    {
        _options = options;
        _plant = plant;
        _time = time;
        _loggers = loggers;
    }

    public bool IsSimulator => string.Equals(_options.Provider, "Simulator", StringComparison.OrdinalIgnoreCase);

    public IThermocoupleSource CreateBoard(FurnaceAcquisitionOptions furnace)
    {
        if (IsSimulator)
        {
            return new SimulatedBoard(_plant.Get(furnace.Code), _time);
        }
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException($"Acquisition provider '{_options.Provider}' is not available on this operating system.");
        }
        return new TcxBoardSource(furnace.BoardIndex, _loggers.CreateLogger<TcxBoardSource>());
    }

    public IFurnaceController CreateController(FurnaceAcquisitionOptions furnace)
    {
        if (IsSimulator)
        {
            return new SimulatedController(_plant.Get(furnace.Code));
        }
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException($"Acquisition provider '{_options.Provider}' is not available on this operating system.");
        }
        return new OpcFurnaceController(furnace, _options.ControllerPollMilliseconds, _loggers.CreateLogger<OpcFurnaceController>());
    }
}

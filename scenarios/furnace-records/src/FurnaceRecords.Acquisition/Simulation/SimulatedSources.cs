using FurnaceRecords.Core;

namespace FurnaceRecords.Acquisition.Simulation;

/// <summary>Board that returns the EMF of the simulated furnace channels, one scan per simulated second.</summary>
public sealed class SimulatedBoard : IThermocoupleSource
{
    private const double ColdJunctionCelsius = 24.0;

    private readonly SimulatedFurnace _furnace;
    private readonly TimeProvider _time;
    private IReadOnlyList<ChannelSetup> _channels = Array.Empty<ChannelSetup>();
    private DateTime _last;
    private uint _sequence;

    public SimulatedBoard(SimulatedFurnace furnace, TimeProvider time)
    {
        _furnace = furnace;
        _time = time;
    }

    public long LostFrames => 0;

    public BoardInfo Open() => new("TCX-3216 (simulated)", 0, "0.0", "0.0", 16, 1, null);

    public void Configure(IReadOnlyList<ChannelSetup> channels, int sampleRateHz) => _channels = channels;

    public void Start() => _last = _furnace.Now;

    public IReadOnlyList<RawFrame> ReadFrames(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var until = _time.GetUtcNow() + timeout;
        while (true)
        {
            var temps = _furnace.TemperaturesSince(_last);
            if (temps.Count > 0)
            {
                _last = temps[^1].AtUtc;
                return temps.Select(t => new RawFrame(
                    ++_sequence,
                    t.AtUtc,
                    0,
                    0,
                    ColdJunctionCelsius,
                    _channels.ToDictionary(
                        c => c.Channel,
                        c => (int)Math.Round((Thermocouple.ToMillivolts(c.Type, t.Celsius[c.Channel] + _furnace.Noise())
                            - Thermocouple.ToMillivolts(c.Type, ColdJunctionCelsius)) * 1000))))
                    .ToList();
            }
            if (_time.GetUtcNow() >= until || cancellationToken.IsCancellationRequested)
            {
                return Array.Empty<RawFrame>();
            }
            Thread.Sleep(20);
        }
    }

    public void Stop()
    {
    }

    public void Dispose()
    {
    }
}

public sealed class SimulatedController : IFurnaceController
{
    private readonly SimulatedFurnace _furnace;

    public SimulatedController(SimulatedFurnace furnace) => _furnace = furnace;

    public void Connect()
    {
    }

    public ControllerState Read() => _furnace.ControllerState();

    public void Dispose()
    {
    }
}

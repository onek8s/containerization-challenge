using System.Collections.Concurrent;
using FurnaceRecords.Core;

namespace FurnaceRecords.Acquisition.Simulation;

/// <summary>Simulated furnaces for development and the lab, keyed by furnace code.</summary>
public sealed class SimulatedPlant
{
    private readonly ConcurrentDictionary<string, SimulatedFurnace> _furnaces = new(StringComparer.OrdinalIgnoreCase);
    private readonly SimulatorOptions _options;
    private readonly TimeProvider _time;

    public SimulatedPlant(SimulatorOptions options, TimeProvider time)
    {
        _options = options;
        _time = time;
    }

    public SimulatedFurnace Get(string code) => _furnaces.GetOrAdd(code, c => new SimulatedFurnace(c, _options, _time));
}

public sealed record SimulatedCycle(string Recipe, double SetpointCelsius, int SoakMinutes, bool Quench);

/// <summary>
/// Furnace model: the control zone heats at a limited rate to the setpoint, each channel follows
/// the zone with its own lag. Time runs <see cref="SimulatorOptions.TimeScale"/> times faster than real time.
/// </summary>
public sealed class SimulatedFurnace
{
    private static readonly double[] LagMinutes = { 1.0, 1.5, 3.0, 4.5, 6.0, 7.5, 9.0, 10.5 };

    private readonly object _sync = new();
    private readonly SimulatorOptions _options;
    private readonly TimeProvider _time;
    private readonly Random _noise;
    private readonly DateTime _realStart;
    private readonly DateTime _simStart;
    private DateTime _simNow;
    private double _zone;
    private readonly double[] _channels = new double[32];

    private SimulatedCycle? _cycle;
    private Phase _phase = Phase.Idle;
    private DateTime _phaseStart;
    private DateTime _holdStart;

    private enum Phase { Idle, Heating, Holding, DoorOpen, Quenched }

    public SimulatedFurnace(string code, SimulatorOptions options, TimeProvider time)
    {
        Code = code;
        _options = options;
        _time = time;
        _noise = new Random(code.GetHashCode());
        _realStart = time.GetUtcNow().UtcDateTime;
        _simStart = _realStart;
        _simNow = _simStart;
        _zone = options.AmbientCelsius;
        Array.Fill(_channels, options.AmbientCelsius);
    }

    public string Code { get; }

    public bool CycleActive { get { lock (_sync) { Advance(); return _phase != Phase.Idle; } } }

    public void StartCycle(SimulatedCycle cycle)
    {
        lock (_sync)
        {
            Advance();
            if (_phase != Phase.Idle)
            {
                throw new InvalidOperationException($"Furnace {Code} is already running a cycle.");
            }
            _cycle = cycle;
            _phase = Phase.Heating;
            _phaseStart = _simNow;
        }
    }

    /// <summary>Simulated time now.</summary>
    public DateTime Now { get { lock (_sync) { Advance(); return _simNow; } } }

    public ControllerState ControllerState()
    {
        lock (_sync)
        {
            Advance();
            return new ControllerState(
                _simNow,
                true,
                _phase != Phase.Idle,
                _cycle?.Recipe,
                _phase == Phase.Idle ? null : _cycle!.SetpointCelsius,
                Math.Round(_zone, 1),
                _phase is Phase.DoorOpen or Phase.Quenched,
                _phase == Phase.Quenched,
                0,
                false);
        }
    }

    /// <summary>Channel temperatures for every simulated second since <paramref name="afterUtc"/>.</summary>
    public IReadOnlyList<(DateTime AtUtc, double[] Celsius)> TemperaturesSince(DateTime afterUtc)
    {
        lock (_sync)
        {
            var list = new List<(DateTime, double[])>();
            var target = SimulatedNow();
            while (_simNow.AddSeconds(1) <= target)
            {
                Step();
                if (_simNow > afterUtc)
                {
                    list.Add((_simNow, (double[])_channels.Clone()));
                }
            }
            return list;
        }
    }

    private DateTime SimulatedNow()
    {
        var real = _time.GetUtcNow().UtcDateTime - _realStart;
        return _simStart + TimeSpan.FromTicks((long)(real.Ticks * _options.TimeScale));
    }

    private void Advance()
    {
        var target = SimulatedNow();
        while (_simNow.AddSeconds(1) <= target)
        {
            Step();
        }
    }

    // One simulated second.
    private void Step()
    {
        _simNow = _simNow.AddSeconds(1);
        var setpoint = _cycle?.SetpointCelsius ?? _options.AmbientCelsius;

        switch (_phase)
        {
            case Phase.Idle:
            case Phase.DoorOpen:
            case Phase.Quenched:
                // Cooling towards ambient, faster with the door open.
                var k = _phase == Phase.Idle ? 1.0 / 3600 : 1.0 / 600;
                _zone += (_options.AmbientCelsius - _zone) * k;
                break;
            default:
                var step = _options.HeatingCelsiusPerMinute / 60.0;
                _zone = Math.Min(setpoint + 1.5, _zone + step);
                if (_zone >= setpoint && _phase == Phase.Heating)
                {
                    _zone = setpoint + 1.5 * Math.Exp(-(_simNow - _phaseStart).TotalMinutes / 30);
                }
                break;
        }

        for (var i = 0; i < _channels.Length; i++)
        {
            // Each position runs slightly hotter or colder than the control zone once heated.
            var offset = ((i * 7) % 9 - 4) * 0.5 * Math.Clamp((_zone - _options.AmbientCelsius) / 400, 0, 1);
            var lag = LagMinutes[i % LagMinutes.Length] * 60;
            _channels[i] += (_zone + offset - _channels[i]) / lag;
        }

        if (_cycle is null)
        {
            return;
        }

        switch (_phase)
        {
            case Phase.Heating when _channels.Take(8).All(c => c >= _cycle.SetpointCelsius - 3):
                _phase = Phase.Holding;
                _holdStart = _simNow;
                break;
            case Phase.Holding when (_simNow - _holdStart).TotalMinutes >= _cycle.SoakMinutes + 5:
                _phase = Phase.DoorOpen;
                _phaseStart = _simNow;
                break;
            case Phase.DoorOpen when _cycle.Quench && (_simNow - _phaseStart).TotalSeconds >= _options.QuenchDelaySeconds:
                _phase = Phase.Quenched;
                _phaseStart = _simNow;
                for (var i = 0; i < _channels.Length; i++) _channels[i] = Math.Min(_channels[i], 60);
                break;
            case Phase.DoorOpen when !_cycle.Quench && (_simNow - _phaseStart).TotalMinutes >= 2:
            case Phase.Quenched when (_simNow - _phaseStart).TotalMinutes >= 2:
                _phase = Phase.Idle;
                _cycle = null;
                break;
        }
    }

    internal double Noise() => (_noise.NextDouble() - 0.5) * 0.4;
}

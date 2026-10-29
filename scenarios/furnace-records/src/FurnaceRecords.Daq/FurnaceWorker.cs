using FurnaceRecords.Acquisition;
using FurnaceRecords.Acquisition.Simulation;
using FurnaceRecords.Core;
using FurnaceRecords.Data;
using FurnaceRecords.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FurnaceRecords.Daq;

/// <summary>Acquisition loop of one furnace: board scans, controller state and the running load.</summary>
internal sealed class FurnaceWorker
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(15);

    private readonly FurnaceAcquisitionOptions _furnace;
    private readonly AcquisitionOptions _options;
    private readonly IAcquisitionFactory _factory;
    private readonly IServiceScopeFactory _scopes;
    private readonly LiveReadings _live;
    private readonly SimulatedPlant? _plant;
    private readonly ILogger _logger;
    private readonly TimeSpan _recordInterval;

    private Dictionary<int, FurnaceChannel> _channels = new();
    private Furnace? _entity;
    private SampleAggregator _aggregator;
    private readonly List<Sample> _pendingSamples = new();
    private readonly List<LoadEvent> _pendingEvents = new();
    private readonly HashSet<int> _openReported = new();
    private Load? _load;
    private ControllerState? _lastState;
    private DateTime _lastFlush = DateTime.UtcNow;
    private DateTime _lastPoll = DateTime.MinValue;
    private Dictionary<int, double> _lastValues = new();
    private Dictionary<int, bool> _lastOpen = new();

    public FurnaceWorker(FurnaceAcquisitionOptions furnace, AcquisitionOptions options, IAcquisitionFactory factory,
        IServiceScopeFactory scopes, LiveReadings live, SimulatedPlant? plant, ILogger logger)
    {
        _furnace = furnace;
        _options = options;
        _factory = factory;
        _scopes = scopes;
        _live = live;
        _plant = plant;
        _logger = logger;
        _recordInterval = TimeSpan.FromSeconds(options.RecordIntervalSeconds);
        _aggregator = new SampleAggregator(_recordInterval);
    }

    public void Run(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                RunOnce(stop);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Acquisition for furnace {Furnace} failed; restarting in 30 s", _furnace.Code);
                Publish("Fault", null, ex.Message);
                stop.WaitHandle.WaitOne(TimeSpan.FromSeconds(30));
            }
        }
    }

    private void RunOnce(CancellationToken stop)
    {
        LoadConfiguration(stop);

        using var board = _factory.CreateBoard(_furnace);
        using var controller = _factory.CreateController(_furnace);

        var info = board.Open();
        board.Configure(_channels.Values.Select(c => new ChannelSetup(c.Channel, c.Type)).ToList(), _options.SampleRateHz);
        controller.Connect();
        board.Start();
        var boardText = $"{info.Model} #{info.Serial} fw {info.Firmware}";
        _logger.LogInformation("Furnace {Furnace} acquiring {Channels} channels on {Board}", _furnace.Code, _channels.Count, boardText);

        ResumeRunningLoad(stop);

        while (!stop.IsCancellationRequested)
        {
            foreach (var frame in board.ReadFrames(TimeSpan.FromMilliseconds(500), stop))
            {
                OnFrame(frame);
            }

            if (DateTime.UtcNow - _lastPoll >= TimeSpan.FromMilliseconds(_options.ControllerPollMilliseconds))
            {
                _lastPoll = DateTime.UtcNow;
                var state = controller.Read();
                if (!state.Connected)
                {
                    controller.Connect();
                }
                OnControllerState(state, stop);
            }

            if (DateTime.UtcNow - _lastFlush >= FlushInterval)
            {
                Flush(stop);
            }

            Publish("Running", boardText, null, board.LostFrames);
        }

        board.Stop();
        Flush(CancellationToken.None);
    }

    private void LoadConfiguration(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FurnaceRecordsContext>();
        _entity = db.Furnaces.AsNoTracking().Include(f => f.Channels).ThenInclude(c => c.Corrections).AsSplitQuery()
            .FirstOrDefault(f => f.Code == _furnace.Code)
            ?? throw new InvalidOperationException($"Furnace {_furnace.Code} is not configured in the database.");
        _channels = _entity.Channels.Where(c => c.Enabled).ToDictionary(c => c.Channel);
    }

    private void ResumeRunningLoad(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FurnaceRecordsContext>();
        _load = db.Loads.AsNoTracking().Include(l => l.Recipe)
            .FirstOrDefault(l => l.FurnaceId == _entity!.Id && l.Status == LoadStatus.Running);
        if (_load is not null)
        {
            _logger.LogWarning("Furnace {Furnace} continues recording load {Load} after restart", _furnace.Code, _load.LoadNumber);
            _pendingEvents.Add(new LoadEvent(DateTime.UtcNow, LoadEventKind.Note, "Recording resumed after service restart"));
        }
    }

    private void OnFrame(RawFrame frame)
    {
        foreach (var (ch, uv) in frame.Microvolts)
        {
            if (!_channels.TryGetValue(ch, out var channel))
            {
                continue;
            }
            var open = frame.IsOpen(ch);
            _lastOpen[ch] = open;
            if (open)
            {
                if (_openReported.Add(ch))
                {
                    _logger.LogWarning("Furnace {Furnace} channel {Channel} ({Name}) open circuit", _furnace.Code, ch, channel.Name);
                    _pendingEvents.Add(new LoadEvent(frame.AtUtc, LoadEventKind.SensorFault, $"Channel {ch} {channel.Name} open circuit"));
                }
                continue;
            }
            _openReported.Remove(ch);

            var raw = Thermocouple.Compensate(channel.Type, uv, frame.ColdJunctionCelsius);
            var corrected = SensorCorrection.Apply(
                channel.Corrections.Select(c => new CorrectionPoint(c.AtCelsius, c.CorrectionCelsius)).ToList(), raw);
            _lastValues[ch] = corrected;

            if (_load is not null)
            {
                _pendingSamples.AddRange(_aggregator.Add(frame.AtUtc, ch, corrected));
            }
        }
    }

    private void OnControllerState(ControllerState s, CancellationToken ct)
    {
        var prev = _lastState;
        _lastState = s;
        if (prev is null || !s.Connected)
        {
            if (s.Connected && s.CycleActive && _load is null)
            {
                StartLoad(s, ct);
            }
            TryAutoStart(s, ct);
            return;
        }

        if (s.CycleActive && !prev.CycleActive)
        {
            StartLoad(s, ct);
        }
        if (_load is not null)
        {
            if (s.DoorOpen != prev.DoorOpen)
                _pendingEvents.Add(new LoadEvent(s.AtUtc, s.DoorOpen ? LoadEventKind.DoorOpen : LoadEventKind.DoorClosed));
            if (s.QuenchDown && !prev.QuenchDown)
                _pendingEvents.Add(new LoadEvent(s.AtUtc, LoadEventKind.QuenchStart));
            if (s.AlarmWord != prev.AlarmWord)
                _pendingEvents.Add(new LoadEvent(s.AtUtc, s.AlarmWord == 0 ? LoadEventKind.AlarmCleared : LoadEventKind.Alarm, $"Alarm word 0x{s.AlarmWord:X4}"));
            if (s.OverTemperatureTripped && !prev.OverTemperatureTripped)
                _pendingEvents.Add(new LoadEvent(s.AtUtc, LoadEventKind.Alarm, "Over-temperature controller tripped"));
            if (s.SetpointCelsius != prev.SetpointCelsius && s.SetpointCelsius is not null && prev.SetpointCelsius is not null)
                _pendingEvents.Add(new LoadEvent(s.AtUtc, LoadEventKind.SetpointChange, $"{prev.SetpointCelsius:0.#} -> {s.SetpointCelsius:0.#} °C"));
        }
        if (!s.CycleActive && prev.CycleActive && _load is not null)
        {
            EndLoad(s.AtUtc, ct);
        }
        TryAutoStart(s, ct);
    }

    private void StartLoad(ControllerState s, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var loads = scope.ServiceProvider.GetRequiredService<LoadService>();
        var next = loads.NextPlannedAsync(_entity!.Id, ct).GetAwaiter().GetResult();
        if (next is null)
        {
            _logger.LogWarning("Furnace {Furnace} started a cycle (recipe {Recipe}) without a planned load; not recorded", _furnace.Code, s.RecipeNumber);
            return;
        }
        if (s.RecipeNumber is not null && !string.Equals(s.RecipeNumber, next.Recipe!.Code, StringComparison.OrdinalIgnoreCase))
        {
            _pendingEvents.Add(new LoadEvent(s.AtUtc, LoadEventKind.Note, $"Controller recipe {s.RecipeNumber}, planned recipe {next.Recipe.Code}"));
        }
        _load = loads.StartAsync(next, s.AtUtc, ct).GetAwaiter().GetResult();
        _aggregator = new SampleAggregator(_recordInterval);
        _logger.LogInformation("Furnace {Furnace} recording load {Load}", _furnace.Code, _load.LoadNumber);
    }

    private void EndLoad(DateTime atUtc, CancellationToken ct)
    {
        _pendingSamples.AddRange(_aggregator.Flush());
        Flush(ct);
        using var scope = _scopes.CreateScope();
        var loads = scope.ServiceProvider.GetRequiredService<LoadService>();
        var result = loads.CompleteAsync(_load!.Id, atUtc, _recordInterval, ct).GetAwaiter().GetResult();
        _logger.LogInformation("Furnace {Furnace} load {Load} completed: {Result}, soak {Soak} min, {Findings} findings",
            _furnace.Code, _load.LoadNumber, result.Passed ? "pass" : "fail", result.SoakMinutes, result.Findings.Count);
        _load = null;
    }

    private void TryAutoStart(ControllerState s, CancellationToken ct)
    {
        if (_plant is null || !_options.Simulator.AutoStartPlannedLoads || s.CycleActive || _load is not null)
        {
            return;
        }
        using var scope = _scopes.CreateScope();
        var next = scope.ServiceProvider.GetRequiredService<LoadService>().NextPlannedAsync(_entity!.Id, ct).GetAwaiter().GetResult();
        if (next is not null)
        {
            var r = next.Recipe!;
            _plant.Get(_furnace.Code).StartCycle(new SimulatedCycle(r.Code, r.SetpointCelsius, r.SoakMinutes, r.MaxQuenchDelaySeconds is not null));
            _logger.LogInformation("Simulator started recipe {Recipe} on furnace {Furnace} for load {Load}", r.Code, _furnace.Code, next.LoadNumber);
        }
    }

    private void Flush(CancellationToken ct)
    {
        _lastFlush = DateTime.UtcNow;
        if (_load is null)
        {
            _pendingSamples.Clear();
            _pendingEvents.Clear();
            return;
        }
        if (_pendingSamples.Count == 0 && _pendingEvents.Count == 0)
        {
            return;
        }
        using var scope = _scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<LoadService>()
            .AppendAsync(_load.Id, _pendingSamples.ToList(), _pendingEvents.ToList(), ct).GetAwaiter().GetResult();
        _pendingSamples.Clear();
        _pendingEvents.Clear();
    }

    private void Publish(string status, string? board, string? error, long lost = 0)
    {
        var s = _lastState;
        _live.Set(new FurnaceLive(
            _furnace.Code,
            status,
            board,
            s?.Connected ?? false,
            s?.CycleActive ?? false,
            _load?.LoadNumber,
            s?.SetpointCelsius,
            s?.ControlCelsius,
            s?.DoorOpen ?? false,
            _channels.Values.OrderBy(c => c.Channel).Select(c => new LiveChannel(
                c.Channel, c.Name, c.Role.ToString(),
                _lastValues.TryGetValue(c.Channel, out var v) ? Math.Round(v, 1) : null,
                _lastOpen.TryGetValue(c.Channel, out var o) && o)).ToList(),
            lost,
            DateTime.UtcNow,
            error));
    }
}

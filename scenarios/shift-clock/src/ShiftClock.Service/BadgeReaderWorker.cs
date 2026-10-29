using System.Diagnostics;
using System.IO.Ports;
using Microsoft.Extensions.Options;
using ShiftClock.Shared;

namespace ShiftClock.Service;

/// <summary>Reads badges from the gate reader on the serial port and records punches.</summary>
public sealed class BadgeReaderWorker : BackgroundService
{
    private readonly ShiftClockOptions _options;
    private readonly PunchStore _store;
    private readonly ReaderState _state;
    private readonly EventLogWriter _eventLog;
    private readonly ILogger<BadgeReaderWorker> _logger;
    private readonly PunchRules _rules;

    public BadgeReaderWorker(
        IOptions<ShiftClockOptions> options,
        PunchStore store,
        ReaderState state,
        EventLogWriter eventLog,
        ILogger<BadgeReaderWorker> logger)
    {
        _options = options.Value;
        _store = store;
        _state = state;
        _eventLog = eventLog;
        _logger = logger;
        _rules = new PunchRules
        {
            DuplicateWindow = TimeSpan.FromSeconds(_options.DuplicateWindowSeconds),
            MaxShiftLength = TimeSpan.FromHours(_options.MaxShiftHours)
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _eventLog.Write(EventLogWriter.ServiceStarted, $"Shift Clock started on terminal {_options.Terminal}.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReadPortAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _state.PortOpen = false;
                _logger.LogError(ex, "Badge reader on {Port} failed", _options.PortName);
                _eventLog.Write(EventLogWriter.ReaderDisconnected,
                    $"Badge reader on {_options.PortName} disconnected: {ex.Message}", EventLogEntryType.Warning);
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task ReadPortAsync(CancellationToken ct)
    {
        using var port = new SerialPort(_options.PortName, _options.BaudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            DtrEnable = true,
            ReadTimeout = SerialPort.InfiniteTimeout
        };
        port.Open();
        _state.PortOpen = true;
        _eventLog.Write(EventLogWriter.ReaderConnected, $"Badge reader connected on {_options.PortName}.");

        var parser = new BadgeFrameParser();
        var buffer = new byte[256];
        await using var registration = ct.Register(() => port.Close());

        while (!ct.IsCancellationRequested)
        {
            var read = await port.BaseStream.ReadAsync(buffer, ct);
            if (read == 0)
                throw new IOException("Port closed.");

            foreach (var badge in parser.Feed(buffer.AsSpan(0, read)))
            {
                var accepted = HandleBadge(badge);
                port.Write([accepted ? _options.AcceptBeep : _options.RejectBeep], 0, 1);
            }
            _state.RejectedFrames = parser.RejectedFrames;
        }
    }

    private bool HandleBadge(string badgeId)
    {
        var now = DateTime.Now;
        var employee = _store.FindEmployee(badgeId);
        if (employee is null)
        {
            _logger.LogWarning("Unknown badge {Badge}", badgeId);
            _eventLog.Write(EventLogWriter.UnknownBadge, $"Unknown badge {badgeId} at {_options.Terminal}.", EventLogEntryType.Warning);
            return false;
        }

        var decision = _rules.Evaluate(_store.LastPunch(employee.EmployeeNumber, now), now, out var direction);
        _state.LastBadgeAt = now;
        _state.LastBadgeName = employee.Name;
        if (decision == PunchDecision.Duplicate)
            return true;

        var punch = new Punch
        {
            BadgeId = badgeId,
            EmployeeNumber = employee.EmployeeNumber,
            EmployeeName = employee.Name,
            Timestamp = now,
            Direction = direction,
            Source = PunchSource.Reader,
            Terminal = _options.Terminal
        };
        _store.Append(punch);
        _logger.LogInformation("{Employee} clocked {Direction}", employee.Name, direction);
        _eventLog.Write(EventLogWriter.PunchRecorded,
            $"{employee.EmployeeNumber} {employee.Name} clocked {direction.ToString().ToLowerInvariant()} at {_options.Terminal}.");
        return true;
    }
}

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using FurnaceRecords.Core;
using Microsoft.Extensions.Logging;

namespace FurnaceRecords.Acquisition.Board;

/// <summary>Thermocouple board read through the Calvera TCX SDK.</summary>
[SupportedOSPlatform("windows")]
internal sealed class TcxBoardSource : IThermocoupleSource
{
    private const int MaxScansPerRead = 256;

    private readonly int _boardIndex;
    private readonly ILogger _logger;
    private ITcxBoard? _board;
    private int[] _channels = Array.Empty<int>();

    public TcxBoardSource(int boardIndex, ILogger logger)
    {
        _boardIndex = boardIndex;
        _logger = logger;
    }

    public long LostFrames => _board?.LostScans ?? 0;

    public BoardInfo Open()
    {
        var type = Type.GetTypeFromCLSID(TcxBoardLib.BoardClsid, throwOnError: true)!;
        _board = (ITcxBoard)Activator.CreateInstance(type)!;
        _board.Open(_boardIndex);

        var cal = _board.CalibrationDate;
        var info = new BoardInfo(
            _board.Model,
            unchecked((uint)_board.SerialNumber),
            _board.FirmwareVersion,
            _board.DriverVersion,
            _board.ChannelCount,
            _board.MaxSampleRate,
            cal.Year > 1900 ? DateOnly.FromDateTime(cal) : null);
        _logger.LogInformation("Opened board {Index}: {Model} serial {Serial}, firmware {Firmware}, driver {Driver}",
            _boardIndex, info.Model, info.Serial, info.Firmware, info.Driver);
        return info;
    }

    public void Configure(IReadOnlyList<ChannelSetup> channels, int sampleRateHz)
    {
        var board = Board;
        var enabled = channels.ToDictionary(c => c.Channel);
        for (var ch = 0; ch < board.ChannelCount; ch++)
        {
            var type = enabled.TryGetValue(ch, out var setup) ? Map(setup.Type) : TcxThermocouple.TypeK;
            board.ConfigureChannel(ch, type, setup is not null);
        }
        board.SetSampleRate(Math.Min(sampleRateHz, board.MaxSampleRate));
        _channels = channels.Select(c => c.Channel).OrderBy(c => c).ToArray();
    }

    public void Start() => Board.Start();

    public IReadOnlyList<RawFrame> ReadFrames(TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var count = Board.ReadScans(MaxScansPerRead, (int)timeout.TotalMilliseconds,
            out var times, out var cjc, out var open, out var microvolts);

        var frames = new List<RawFrame>(count);
        for (var i = 0; i < count; i++)
        {
            var values = new Dictionary<int, int>(_channels.Length);
            foreach (var ch in _channels)
            {
                values[ch] = microvolts[i, ch];
            }
            frames.Add(new RawFrame(
                0,
                DateTime.SpecifyKind(times[i], DateTimeKind.Utc),
                0,
                unchecked((uint)open[i]),
                cjc[i],
                values));
        }
        return frames;
    }

    public void Stop()
    {
        if (_board?.State == TcxState.Running)
        {
            _board.Stop();
        }
    }

    public void Dispose()
    {
        if (_board is null)
        {
            return;
        }
        try
        {
            Stop();
            _board.Close();
        }
        catch (COMException ex)
        {
            _logger.LogWarning(ex, "Closing board {Index} failed", _boardIndex);
        }
        Marshal.FinalReleaseComObject(_board);
        _board = null;
    }

    private ITcxBoard Board => _board ?? throw new InvalidOperationException("Board is not open.");

    private static TcxThermocouple Map(ThermocoupleType type) => type switch
    {
        ThermocoupleType.K => TcxThermocouple.TypeK,
        ThermocoupleType.N => TcxThermocouple.TypeN,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}

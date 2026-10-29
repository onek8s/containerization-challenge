using System.Collections.Concurrent;

namespace FurnaceRecords.Daq;

public sealed record LiveChannel(int Channel, string Name, string Role, double? Celsius, bool Open);

public sealed record FurnaceLive(
    string Code,
    string Status,
    string? Board,
    bool ControllerConnected,
    bool CycleActive,
    string? LoadNumber,
    double? SetpointCelsius,
    double? ControlCelsius,
    bool DoorOpen,
    IReadOnlyList<LiveChannel> Channels,
    long LostScans,
    DateTime UpdatedUtc,
    string? LastError);

public sealed class LiveReadings
{
    private readonly ConcurrentDictionary<string, FurnaceLive> _furnaces = new(StringComparer.OrdinalIgnoreCase);

    public void Set(FurnaceLive live) => _furnaces[live.Code] = live;

    public FurnaceLive? Get(string code) => _furnaces.TryGetValue(code, out var f) ? f : null;

    public IReadOnlyList<FurnaceLive> All() => _furnaces.Values.OrderBy(f => f.Code).ToList();
}

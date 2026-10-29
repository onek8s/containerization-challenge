namespace FurnaceRecords.Core;

public enum ChannelRole
{
    Control,
    OverTemperature,
    Load,
    Recorder,
    Survey,
}

/// <summary>One recorded value of one channel.</summary>
public readonly record struct Sample(DateTime AtUtc, int Channel, double Celsius);

public enum LoadEventKind
{
    CycleStart,
    CycleEnd,
    DoorOpen,
    DoorClosed,
    QuenchStart,
    Alarm,
    AlarmCleared,
    SensorFault,
    SetpointChange,
    Note,
}

public readonly record struct LoadEvent(DateTime AtUtc, LoadEventKind Kind, string? Detail = null);

/// <summary>
/// Reduces fast channel readings to one mean value per channel and record interval.
/// </summary>
public sealed class SampleAggregator
{
    private readonly TimeSpan _interval;
    private readonly Dictionary<int, (double Sum, int Count)> _buckets = new();
    private DateTime _bucketStart = DateTime.MinValue;

    public SampleAggregator(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
        _interval = interval;
    }

    public TimeSpan Interval => _interval;

    /// <summary>Adds a reading and returns the completed interval values when a new interval starts.</summary>
    public IReadOnlyList<Sample> Add(DateTime atUtc, int channel, double celsius)
    {
        var start = new DateTime(atUtc.Ticks - atUtc.Ticks % _interval.Ticks, DateTimeKind.Utc);
        IReadOnlyList<Sample> completed = Array.Empty<Sample>();
        if (start != _bucketStart)
        {
            completed = Flush();
            _bucketStart = start;
        }

        _buckets.TryGetValue(channel, out var b);
        _buckets[channel] = (b.Sum + celsius, b.Count + 1);
        return completed;
    }

    public IReadOnlyList<Sample> Flush()
    {
        if (_buckets.Count == 0)
        {
            return Array.Empty<Sample>();
        }
        var list = _buckets
            .OrderBy(kv => kv.Key)
            .Select(kv => new Sample(_bucketStart, kv.Key, Math.Round(kv.Value.Sum / kv.Value.Count, 1)))
            .ToList();
        _buckets.Clear();
        return list;
    }
}

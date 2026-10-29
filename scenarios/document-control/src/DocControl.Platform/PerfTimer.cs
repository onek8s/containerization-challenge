using Litware.DocControl.Platform.Interop;

namespace Litware.DocControl.Platform;

/// <summary>High resolution timer for the scan and build statistics.</summary>
public readonly struct PerfTimer
{
    private static readonly long Frequency = QueryFrequency();
    private readonly long _start;

    private PerfTimer(long start) => _start = start;

    public static PerfTimer Start()
    {
        NativeMethods.QueryPerformanceCounter(out var now);
        return new PerfTimer(now);
    }

    public TimeSpan Elapsed
    {
        get
        {
            NativeMethods.QueryPerformanceCounter(out var now);
            return TimeSpan.FromSeconds((now - _start) / (double)Frequency);
        }
    }

    private static long QueryFrequency()
    {
        NativeMethods.QueryPerformanceFrequency(out var f);
        return f == 0 ? 1 : f;
    }
}

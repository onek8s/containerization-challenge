using Litware.DocControl.Platform.Interop;

namespace Litware.DocControl.Platform;

/// <summary>
/// Keeps the machine from going to sleep while a long package build is running.
/// Dispose to release. Must be disposed on the thread that created it.
/// </summary>
public sealed class PowerRequest : IDisposable
{
    private bool _disposed;

    public PowerRequest()
    {
        NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS | NativeMethods.ES_SYSTEM_REQUIRED);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS);
    }
}

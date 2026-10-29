using System.Runtime.Versioning;
using System.Windows.Threading;

namespace StudyData.Documents.Wpf;

/// <summary>
/// WPF needs an STA thread with a running dispatcher. One render thread per process serves all requests;
/// WPF visuals and documents never leave it.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WpfRenderThread : IDisposable
{
    private readonly Thread _thread;
    private readonly TaskCompletionSource<Dispatcher> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public WpfRenderThread()
    {
        _thread = new Thread(() =>
        {
            _ready.SetResult(Dispatcher.CurrentDispatcher);
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "WPF render"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    private Dispatcher Dispatcher => _ready.Task.GetAwaiter().GetResult();

    public Task<T> InvokeAsync<T>(Func<T> func, CancellationToken ct = default) =>
        Dispatcher.InvokeAsync(func, DispatcherPriority.Normal, ct).Task;

    public void Dispose()
    {
        if (_ready.Task.IsCompletedSuccessfully)
        {
            Dispatcher.InvokeShutdown();
        }
    }
}

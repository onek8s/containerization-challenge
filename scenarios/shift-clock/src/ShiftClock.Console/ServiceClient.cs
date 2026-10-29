using System.IO.Pipes;
using System.Security.Principal;
using ShiftClock.Shared;

namespace ShiftClock.Console;

/// <summary>Talks to the Shift Clock service over its local named pipe.</summary>
public sealed class ServiceClient : IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private NamedPipeClientStream? _pipe;

    public string? CallerName { get; private set; }
    public bool IsSupervisor { get; private set; }

    public async Task<SupervisorResponse> SendAsync(SupervisorRequest request, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    var pipe = await ConnectAsync(ct);
                    await PipeProtocol.WriteAsync(pipe, request, ct);
                    var response = await PipeProtocol.ReadAsync<SupervisorResponse>(pipe, ct)
                        ?? throw new IOException("The service closed the connection.");
                    CallerName = response.CallerName;
                    IsSupervisor = response.CallerIsSupervisor;
                    return response;
                }
                catch (IOException) when (attempt == 0)
                {
                    await ResetAsync();
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<NamedPipeClientStream> ConnectAsync(CancellationToken ct)
    {
        if (_pipe is { IsConnected: true })
            return _pipe;

        await ResetAsync();
        // Impersonation lets the service check group membership and write payroll
        // exports to the file share under the signed-in supervisor's account.
        _pipe = new NamedPipeClientStream(".", PipeProtocol.PipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation);
        await _pipe.ConnectAsync(5000, ct);
        return _pipe;
    }

    private async Task ResetAsync()
    {
        if (_pipe is not null)
            await _pipe.DisposeAsync();
        _pipe = null;
    }

    public async ValueTask DisposeAsync() => await ResetAsync();
}

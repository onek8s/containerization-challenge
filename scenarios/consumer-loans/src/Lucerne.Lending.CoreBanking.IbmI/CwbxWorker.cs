using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace Lucerne.Lending.CoreBanking.IbmI;

/// <summary>
/// One signed-on AS400System object with its own STA thread. All cwbx objects created for a
/// session are used only on that thread.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class CwbxWorker : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;
    private readonly CoreOptions _options;
    private readonly ILogger _log;
    private dynamic? _system;
    private RecordCodec? _codec;
    private DateTime _lastUse = DateTime.UtcNow;

    public CwbxWorker(int number, CoreOptions options, ILogger log)
    {
        _options = options;
        _log = log;
        _thread = new Thread(Run) { IsBackground = true, Name = $"cwbx-{number}" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public Task<T> InvokeAsync<T>(Func<dynamic, RecordCodec, T> work, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = ct.Register(() => tcs.TrySetCanceled(ct));
        _queue.Add(() =>
        {
            if (tcs.Task.IsCompleted) return;
            try
            {
                EnsureSignedOn();
                tcs.TrySetResult(work(_system!, _codec!));
                _lastUse = DateTime.UtcNow;
            }
            catch (COMException ex)
            {
                string detail = _system != null ? (string)Cwbx.ErrorText(_system) : "";
                _log.LogError(ex, "cwbx call failed on {System}: {Detail}", _options.SystemName, detail);
                ResetSession();
                tcs.TrySetException(new CoreBankingException($"Core call failed: {ex.Message} {detail}".Trim(), ex));
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    private void EnsureSignedOn()
    {
        if (_system != null)
        {
            // host server connections are dropped by the IBM i after the inactivity timeout (QZRCSRVS, 30 min)
            if (DateTime.UtcNow - _lastUse < TimeSpan.FromMinutes(20) && (int)_system.IsConnected(Cwbx.ServiceRemoteCmd) != 0) return;
            ResetSession();
        }

        dynamic system = Cwbx.Create(Cwbx.AS400System);
        system.Define(_options.SystemName);
        system.PromptMode = Cwbx.PromptNever;
        if (_options.UseKerberos)
        {
            // System connection in ACS is configured with "Use Kerberos principal name, no prompting";
            // the ticket of the service account (app pool / Windows service identity) is used.
            system.DefaultUserMode = Cwbx.DefaultUserUseWinLogon;
        }
        else
        {
            system.UserID = _options.UserId;
            system.Password = _options.Password;
        }
        system.Signon();
        system.Connect(Cwbx.ServiceRemoteCmd);
        _system = system;
        _codec ??= new RecordCodec(CoreLayouts.CodePage);
        _log.LogInformation("Signed on to {System} on thread {Thread}", _options.SystemName, Thread.CurrentThread.Name);
    }

    private void ResetSession()
    {
        try { _system?.Disconnect(Cwbx.ServiceAll); } catch (COMException) { }
        Cwbx.Release(_system);
        _system = null;
    }

    private void Run()
    {
        foreach (var action in _queue.GetConsumingEnumerable()) action();
        ResetSession();
        _codec?.Dispose();
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(10));
    }
}

[SupportedOSPlatform("windows")]
internal sealed class CwbxPool : IDisposable
{
    private readonly CwbxWorker[] _workers;
    private readonly SemaphoreSlim[] _locks;
    private int _next;

    public CwbxPool(CoreOptions options, ILogger<CwbxPool> log)
    {
        var size = Math.Max(1, options.MaxConnections);
        _workers = Enumerable.Range(1, size).Select(i => new CwbxWorker(i, options, log)).ToArray();
        _locks = _workers.Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    }

    public async Task<T> RunAsync<T>(Func<dynamic, RecordCodec, T> work, CancellationToken ct)
    {
        var index = (int)((uint)Interlocked.Increment(ref _next) % _workers.Length);
        await _locks[index].WaitAsync(ct);
        try
        {
            return await _workers[index].InvokeAsync(work, ct);
        }
        finally
        {
            _locks[index].Release();
        }
    }

    public void Dispose()
    {
        foreach (var w in _workers) w.Dispose();
    }
}

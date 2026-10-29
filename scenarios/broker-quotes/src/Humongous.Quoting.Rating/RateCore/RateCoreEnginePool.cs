using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Humongous.Quoting.Domain;
using Humongous.Quoting.Rating.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Humongous.Quoting.Rating.RateCore;

/// <summary>
/// Runs RateCore on dedicated single-threaded apartment threads. RateCore is an apartment
/// threaded VB6 component: each thread creates its own engine, and every call for that engine
/// must be made on the thread that created it. Module level data in RateCore (the cached rate
/// tables) is per thread, so each worker loads the tables once.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RateCoreEnginePool : IRatingEngine, IRatingEngineStatus, IDisposable
{
    private readonly RatingOptions _options;
    private readonly ILogger<RateCoreEnginePool> _logger;
    private readonly BlockingCollection<WorkItem> _queue = new();
    private readonly List<Thread> _threads = [];
    private readonly CancellationTokenSource _stopping = new();
    private int _busy;
    private long _calls;
    private string _engineVersion = "";
    private string? _lastError;

    public RateCoreEnginePool(IOptions<RatingOptions> options, ILogger<RateCoreEnginePool> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (Environment.Is64BitProcess)
            throw new PlatformNotSupportedException(
                "RateCore.dll is a 32-bit in-process COM server. Run this application as a 32-bit process " +
                "(win-x86 publish; IIS application pool with 'Enable 32-Bit Applications').");

        for (var i = 0; i < Math.Max(1, _options.Workers); i++)
        {
            var thread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = $"RateCore-{i + 1}"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            _threads.Add(thread);
        }
    }

    public string Name => "RateCore";

    public Task<RatingResult> RateAsync(RatingRequest request, CancellationToken cancellationToken = default)
    {
        var item = new WorkItem(request, new TaskCompletionSource<RatingResult>(TaskCreationOptions.RunContinuationsAsynchronously));
        _queue.Add(item, cancellationToken);

        var timeout = TimeSpan.FromSeconds(_options.CallTimeoutSeconds);
        return item.Completion.Task.WaitAsync(timeout, cancellationToken);
    }

    public RatingEngineState GetState() => new(
        Name,
        _engineVersion,
        _threads.Count,
        Volatile.Read(ref _busy),
        _queue.Count,
        Interlocked.Read(ref _calls),
        _lastError);

    private void WorkerLoop()
    {
        _Engine? engine = null;
        var callsOnEngine = 0;
        try
        {
            foreach (var item in _queue.GetConsumingEnumerable(_stopping.Token))
            {
                Interlocked.Increment(ref _busy);
                try
                {
                    if (engine is null || callsOnEngine >= _options.RecycleAfterCalls)
                    {
                        Release(ref engine);
                        engine = CreateEngine();
                        callsOnEngine = 0;
                    }

                    var result = Rate(engine, item.Request);
                    callsOnEngine++;
                    Interlocked.Increment(ref _calls);
                    item.Completion.TrySetResult(result);
                }
                catch (COMException ex)
                {
                    _lastError = RateCoreErrors.Describe(ex.HResult);
                    _logger.LogError(ex, "RateCore failed for {Quote}: {Error}", item.Request.QuoteReference, _lastError);
                    item.Completion.TrySetException(new RatingException(_lastError, ex.HResult, ex));
                    // An engine that raised a COM error may hold half-loaded tables; start again.
                    Release(ref engine);
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;
                    _logger.LogError(ex, "Rating failed for {Quote}", item.Request.QuoteReference);
                    item.Completion.TrySetException(ex as RatingException ?? new RatingException(ex.Message, 0, ex));
                }
                finally
                {
                    Interlocked.Decrement(ref _busy);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            Release(ref engine);
        }
    }

    private _Engine CreateEngine()
    {
        var type = Type.GetTypeFromProgID(_options.ProgId, throwOnError: false)
            ?? throw new RatingException($"COM class '{_options.ProgId}' is not registered. Register RateCore.dll with the 32-bit regsvr32.");
        var engine = (_Engine)Activator.CreateInstance(type)!;
        if (!engine.Initialise(_options.TablesPath))
        {
            var error = engine.get_LastError();
            Marshal.FinalReleaseComObject(engine);
            throw new RatingException($"RateCore could not load the rate tables from {_options.TablesPath}: {error}");
        }
        _engineVersion = engine.get_Version();
        _logger.LogInformation("RateCore {Version} started on {Thread} with tables {Path}",
            _engineVersion, Thread.CurrentThread.Name, _options.TablesPath);
        return engine;
    }

    private RatingResult Rate(_Engine engine, RatingRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var risk = engine.NewRisk(request.Product.RateCoreProduct, request.EffectiveDate.ToDateTime(TimeOnly.MinValue));
        try
        {
            RateCoreMapper.Load(risk, request);
            var outcome = risk.Calculate();
            if (outcome < 0)
                throw new RatingException($"RateCore could not rate {request.QuoteReference}: {risk.get_ErrorText()}", outcome);

            var result = RateCoreMapper.Read(risk, _engineVersion);
            _logger.LogDebug("Rated {Quote} with tables {Tables} in {Elapsed} ms (outcome {Outcome})",
                request.QuoteReference, result.TableVersion, stopwatch.ElapsedMilliseconds, outcome);
            return result with { Duration = stopwatch.Elapsed };
        }
        finally
        {
            Marshal.FinalReleaseComObject(risk);
        }
    }

    private static void Release(ref _Engine? engine)
    {
        if (engine is null) return;
        try
        {
            engine.Terminate();
        }
        catch (COMException)
        {
        }
        Marshal.FinalReleaseComObject(engine);
        engine = null;
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _stopping.CancelAfter(TimeSpan.FromSeconds(10));
        foreach (var thread in _threads)
            thread.Join(TimeSpan.FromSeconds(15));
        _stopping.Dispose();
        _queue.Dispose();
    }

    private sealed record WorkItem(RatingRequest Request, TaskCompletionSource<RatingResult> Completion);
}

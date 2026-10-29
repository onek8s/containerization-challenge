using System.Runtime.Versioning;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace StudyData.Calculation.Excel;

/// <summary>
/// Fixed pool of Excel instances shared by all requests of the process. Instances are created on first
/// use, recycled after a number of calculations and replaced when they hang.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ExcelPool : IDisposable
{
    private readonly CalculationOptions _options;
    private readonly ILogger<ExcelPool> _log;
    private readonly Channel<int> _free;
    private readonly ExcelInstance?[] _instances;
    private int _created;

    public ExcelPool(IOptions<CalculationOptions> options, ILogger<ExcelPool> log)
    {
        _options = options.Value;
        _log = log;
        _instances = new ExcelInstance?[Math.Max(1, _options.PoolSize)];
        _free = Channel.CreateBounded<int>(_instances.Length);
        for (var i = 0; i < _instances.Length; i++)
        {
            _free.Writer.TryWrite(i);
        }
    }

    public int Size => _instances.Length;

    public IReadOnlyList<(int Number, int ProcessId, int Calculations)> Status() =>
        _instances.Where(i => i is not null).Select(i => (i!.Number, i.ProcessId, i.Calculations)).ToList();

    internal async Task<T> UseAsync<T>(Func<ExcelInstance, T> work, CancellationToken ct)
    {
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
        wait.CancelAfter(_options.QueueTimeout);
        int slot;
        try
        {
            slot = await _free.Reader.ReadAsync(wait.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new Core.Services.DerivationException("All calculation engines are busy. Try saving again in a moment.");
        }

        try
        {
            var instance = _instances[slot];
            if (instance is null || instance.Broken || instance.Calculations >= _options.MaxCalculationsPerInstance)
            {
                instance?.Dispose();
                instance = _instances[slot] = new ExcelInstance(Interlocked.Increment(ref _created), _log);
            }

            try
            {
                return work(instance);
            }
            catch (TimeoutException ex)
            {
                _log.LogError(ex, "Excel #{Number} hung, replacing it", instance.Number);
                instance.Kill();
                _instances[slot] = null;
                throw new Core.Services.DerivationException("The calculation did not finish in time.", ex);
            }
        }
        finally
        {
            _free.Writer.TryWrite(slot);
        }
    }

    public void Dispose()
    {
        foreach (var i in _instances)
        {
            i?.Dispose();
        }
    }
}

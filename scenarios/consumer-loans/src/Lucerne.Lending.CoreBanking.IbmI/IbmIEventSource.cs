using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lucerne.Lending.CoreBanking.IbmI;

/// <summary>
/// Reads loan events from data queue LNLIB/LNEVTQ (keyless FIFO, written by the end-of-day job LNEOD
/// and by payment posting). Entries are removed when read, so only one reader may run.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class IbmIEventSource : ICoreEventSource, IDisposable
{
    private readonly CwbxWorker _worker;
    private readonly CoreOptions _options;
    private readonly ILogger<IbmIEventSource> _log;
    private dynamic? _queue;

    public IbmIEventSource(IOptions<CoreOptions> options, ILogger<IbmIEventSource> log)
    {
        _options = options.Value;
        _log = log;
        _worker = new CwbxWorker(0, _options, log);
    }

    public Task<CoreLoanEvent?> ReceiveAsync(TimeSpan wait, CancellationToken ct = default) =>
        _worker.InvokeAsync<CoreLoanEvent?>((system, codec) =>
        {
            if (_queue == null)
            {
                dynamic q = Cwbx.Create(Cwbx.DataQueue);
                q.system = system;
                q.LibraryName = _options.EventQueueLibrary;
                q.QueueName = _options.EventQueue;
                _queue = q;
            }

            object? data;
            try
            {
                // Read(wait seconds) returns the entry bytes, or an empty array on timeout
                data = _queue.Read((int)Math.Max(0, wait.TotalSeconds));
            }
            catch (COMException)
            {
                Cwbx.Release(_queue);
                _queue = null;
                throw;
            }

            if (data is not byte[] bytes || bytes.Length == 0) return null;
            var f = codec.Decode(CoreLayouts.EventEntry, Pad(bytes, CoreLayouts.EventEntry.Length));
            var date = f.Date("DATE") ?? DateOnly.FromDateTime(DateTime.Today);
            var time = ((long)f.Dec("TIME")).ToString("000000", CultureInfo.InvariantCulture);
            var ts = date.ToDateTime(TimeOnly.ParseExact(time, "HHmmss", CultureInfo.InvariantCulture));
            var evt = new CoreLoanEvent(f["TYPE"], f["ACCOUNT"], ts, f.Dec("AMOUNT"), f.Dec("ARREARS"), f.Int("DPD"),
                Convert.ToHexString(bytes));
            _log.LogDebug("LNEVTQ {Type} {Account}", evt.EventType, evt.AccountNumber);
            return evt;
        }, ct);

    private static byte[] Pad(byte[] bytes, int length)
    {
        if (bytes.Length >= length) return bytes;
        var padded = new byte[length];
        Array.Fill(padded, (byte)0x40); // EBCDIC blank
        Buffer.BlockCopy(bytes, 0, padded, 0, bytes.Length);
        return padded;
    }

    public void Dispose()
    {
        if (_queue != null) _worker.InvokeAsync<object?>((_, _) => { Cwbx.Release(_queue); return null; }, CancellationToken.None).Wait(5000);
        _worker.Dispose();
    }
}

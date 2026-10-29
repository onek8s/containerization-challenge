using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace StudyData.Calculation.Excel;

/// <summary>
/// One Excel.Application automation server on its own STA thread. All calls to the instance run on that
/// thread. Workbooks stay open between calculations, which is what makes a derivation take milliseconds
/// instead of seconds.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class ExcelInstance : IDisposable
{
    private const int XlCalculationManual = -4135;
    private const int MsoAutomationSecurityForceDisable = 3;

    private readonly BlockingCollection<Action> _work = new();
    private readonly Thread _thread;
    private readonly ILogger _log;
    private readonly Dictionary<string, dynamic> _workbooks = new(StringComparer.OrdinalIgnoreCase);
    private dynamic? _app;

    public ExcelInstance(int number, ILogger log)
    {
        Number = number;
        _log = log;
        _thread = new Thread(Run) { IsBackground = true, Name = $"Excel #{number}" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        Invoke(Start, TimeSpan.FromSeconds(60));
    }

    public int Number { get; }
    public int ProcessId { get; private set; }
    public int Calculations { get; private set; }
    public bool Broken { get; private set; }

    public T Invoke<T>(Func<T> func, TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _work.Add(() =>
        {
            try
            {
                tcs.SetResult(func());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        if (!tcs.Task.Wait(timeout))
        {
            Broken = true;
            throw new TimeoutException($"Excel #{Number} (pid {ProcessId}) did not answer in {timeout.TotalSeconds:0} s.");
        }

        return tcs.Task.GetAwaiter().GetResult();
    }

    private void Invoke(Action action, TimeSpan timeout) => Invoke(() => { action(); return 0; }, timeout);

    private void Run()
    {
        foreach (var item in _work.GetConsumingEnumerable())
        {
            item();
        }
    }

    private void Start()
    {
        var type = Type.GetTypeFromProgID("Excel.Application", throwOnError: false)
            ?? throw new InvalidOperationException("Microsoft Excel is not installed (ProgID Excel.Application is not registered).");
        _app = Activator.CreateInstance(type)!;
        _app.Visible = false;
        _app.DisplayAlerts = false;
        _app.ScreenUpdating = false;
        _app.EnableEvents = false;
        _app.Interactive = false;
        _app.AskToUpdateLinks = false;
        _app.AutomationSecurity = MsoAutomationSecurityForceDisable;

        int hwnd = _app.Hwnd;
        GetWindowThreadProcessId(new IntPtr(hwnd), out var pid);
        ProcessId = (int)pid;
        string version = _app.Version;
        _log.LogInformation("Excel #{Number} started, version {Version}, pid {Pid}", Number, version, ProcessId);
    }

    public dynamic Workbook(string path)
    {
        if (_workbooks.TryGetValue(path, out var wb))
        {
            return wb;
        }

        // Open(FileName, UpdateLinks, ReadOnly)
        wb = _app!.Workbooks.Open(path, 0, true);
        _app.Calculation = XlCalculationManual;
        _workbooks[path] = wb;
        return wb;
    }

    public IReadOnlyDictionary<string, string?> Calculate(string path, IReadOnlyDictionary<string, object?> inputs,
        IReadOnlyCollection<string> outputs, List<string> messages)
    {
        var wb = Workbook(path);
        foreach (var (name, value) in inputs)
        {
            dynamic range = Range(wb, name);
            range.Value2 = ToExcel(value);
            Marshal.ReleaseComObject(range);
        }

        _app!.Calculate();
        Calculations++;

        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in outputs)
        {
            dynamic range = Range(wb, name);
            object? v = range.Value2;
            Marshal.ReleaseComObject(range);
            result[name] = FromExcel(name, v, messages);
        }

        return result;
    }

    /// <summary>Runs report automation (fill ranges, recalculate, save, export) on the instance thread.</summary>
    public T Automate<T>(Func<dynamic, T> work, TimeSpan timeout) => Invoke(() => work(_app!), timeout);

    private static dynamic Range(dynamic workbook, string name)
    {
        try
        {
            return workbook.Names.Item(name).RefersToRange;
        }
        catch (COMException ex)
        {
            throw new Core.Services.DerivationException($"The workbook has no named range {name}.", ex);
        }
    }

    private static object ToExcel(object? value) => value switch
    {
        null => "",
        decimal d => (double)d,
        DateOnly date => date.ToDateTime(TimeOnly.MinValue).ToOADate(),
        bool b => b,
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
    };

    private static string? FromExcel(string name, object? value, List<string> messages)
    {
        switch (value)
        {
            case null:
                return null;
            case string s:
                return s.Length == 0 ? null : s;
            case double d:
                return d.ToString("R", CultureInfo.InvariantCulture);
            case bool b:
                return b ? "1" : "0";
            case int error:
                // CVErr values come back as Int32 HRESULTs (#DIV/0! = -2146826281, #VALUE! = -2146826273 ...).
                messages.Add($"{name}: workbook returned error {ErrorText(error)}");
                return null;
            default:
                return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }

    private static string ErrorText(int code) => code switch
    {
        -2146826281 => "#DIV/0!",
        -2146826246 => "#N/A",
        -2146826259 => "#NAME?",
        -2146826288 => "#NULL!",
        -2146826252 => "#NUM!",
        -2146826265 => "#REF!",
        -2146826273 => "#VALUE!",
        _ => code.ToString(CultureInfo.InvariantCulture)
    };

    public void Dispose()
    {
        try
        {
            if (!Broken)
            {
                Invoke(Quit, TimeSpan.FromSeconds(15));
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Excel #{Number} did not quit", Number);
        }
        finally
        {
            _work.CompleteAdding();
            Kill();
        }
    }

    private void Quit()
    {
        foreach (var wb in _workbooks.Values)
        {
            wb.Close(false);
            Marshal.FinalReleaseComObject(wb);
        }

        _workbooks.Clear();
        if (_app is not null)
        {
            _app.Quit();
            Marshal.FinalReleaseComObject(_app);
            _app = null;
        }
    }

    /// <summary>Excel is out-of-process; a hung instance is ended by its process id.</summary>
    public void Kill()
    {
        if (ProcessId == 0)
        {
            return;
        }

        try
        {
            using var p = Process.GetProcessById(ProcessId);
            if (!p.HasExited && p.ProcessName.Equals("EXCEL", StringComparison.OrdinalIgnoreCase))
            {
                p.Kill();
                _log.LogWarning("Excel #{Number} pid {Pid} ended", Number, ProcessId);
            }
        }
        catch (ArgumentException)
        {
            // Already gone.
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}

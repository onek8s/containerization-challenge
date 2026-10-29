using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudyData.Core.Coding;

namespace StudyData.Coding.Coderis;

/// <summary>
/// Coderis AutoCode 6 through COM. The engine is single-threaded, so one surrogate instance is created
/// on a dedicated STA thread and every call is queued to that thread.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class CoderisCodingEngine : ICodingEngine, IDisposable
{
    private const int FlagSynonyms = 0x1;
    private const int FlagCurrentOnly = 0x4;

    private readonly CodingOptions _options;
    private readonly ILogger<CoderisCodingEngine> _log;
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;
    private readonly Dictionary<string, string> _versions = new(StringComparer.OrdinalIgnoreCase);
    private IAutoCoder? _coder;

    public CoderisCodingEngine(IOptions<CodingOptions> options, ILogger<CoderisCodingEngine> log)
    {
        _options = options.Value;
        _log = log;
        _thread = new Thread(() =>
        {
            foreach (var work in _queue.GetConsumingEnumerable())
            {
                work();
            }
        })
        { IsBackground = true, Name = "Coderis" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public string DictionaryVersion(string dictionary) => Run(() =>
    {
        Open(dictionary);
        return _versions[dictionary];
    });

    public IReadOnlyList<CodingCandidate> Search(string dictionary, string verbatim, int maxCandidates) => Run(() =>
    {
        Open(dictionary);
        var raw = _coder!.Code(dictionary, verbatim, maxCandidates, FlagSynonyms | FlagCurrentOnly);
        if (raw is not object[] rows)
        {
            var error = _coder.LastError;
            if (error != 0)
            {
                throw new InvalidOperationException($"Coderis error {error} coding '{verbatim}'.");
            }

            return (IReadOnlyList<CodingCandidate>)Array.Empty<CodingCandidate>();
        }

        return rows.OfType<string>().Select(Parse).Where(c => c is not null).Select(c => c!).ToList();
    });

    private void Open(string dictionary)
    {
        if (_coder is null)
        {
            var clsid = CoderisIds.Clsid;
            var iid = typeof(IAutoCoder).GUID;
            _coder = (IAutoCoder)Ole32.CoCreateInstance(ref clsid, null, Ole32.ClsctxLocalServer, ref iid);
            _log.LogInformation("Coderis AutoCode started in the COM surrogate");
        }

        if (_versions.ContainsKey(dictionary))
        {
            return;
        }

        var version = dictionary.ToUpperInvariant() switch
        {
            "MEDDRA" => _options.MedDraVersion,
            "WHODRUG" => _options.WhoDrugVersion,
            _ => throw new ArgumentException($"Unknown dictionary {dictionary}.")
        };
        _coder.OpenDictionary(dictionary, version, _options.LicenceFile);
        _versions[dictionary] = _coder.DictionaryInfo(dictionary);
        _log.LogInformation("Coderis dictionary {Dictionary} opened: {Info}", dictionary, _versions[dictionary]);
    }

    private static CodingCandidate? Parse(string row)
    {
        var p = row.Split('\t');
        if (p.Length < 6 || !int.TryParse(p[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score))
        {
            return null;
        }

        return new CodingCandidate(p[0], p[1], p[2], p[3], p[4], score);
    }

    private T Run<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() =>
        {
            try
            {
                tcs.SetResult(func());
            }
            catch (COMException ex) when ((uint)ex.HResult == 0x800706BA || (uint)ex.HResult == 0x800706BE)
            {
                // RPC server unavailable / call failed: the surrogate died. Start a new one next time.
                _coder = null;
                _versions.Clear();
                tcs.SetException(new InvalidOperationException("The coding engine stopped. Try again.", ex));
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task.GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        if (_coder is not null)
        {
            Run(() =>
            {
                _coder.CloseAll();
                Marshal.FinalReleaseComObject(_coder);
                _coder = null;
                return 0;
            });
        }

        _queue.CompleteAdding();
    }
}

using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lucerne.Lending.CoreBanking.IbmI;

/// <summary>Calls RPG programs in the lending library through the cwbx remote command server.</summary>
[SupportedOSPlatform("windows")]
internal sealed class ProgramCaller
{
    private readonly CwbxPool _pool;
    private readonly CoreOptions _options;
    private readonly ILogger<ProgramCaller> _log;

    public ProgramCaller(CwbxPool pool, IOptions<CoreOptions> options, ILogger<ProgramCaller> log)
    {
        _pool = pool;
        _options = options.Value;
        _log = log;
    }

    /// <summary>Calls library/program with an input parameter and an output parameter.</summary>
    public Task<Dictionary<string, string>> CallAsync(string program, RecordLayout input, IReadOnlyDictionary<string, object?> values,
        RecordLayout output, CancellationToken ct, string? library = null)
    {
        var lib = library ?? _options.LendingLibrary;
        return _pool.RunAsync((system, codec) =>
        {
            var started = DateTime.UtcNow;
            dynamic prog = Cwbx.Create(Cwbx.Program);
            dynamic parms = Cwbx.Create(Cwbx.ProgramParameters);
            try
            {
                prog.system = system;
                prog.LibraryName = lib;
                prog.ProgramName = program;
                parms.Append("IN", Cwbx.ParameterInput, input.Length);
                parms.Append("OUT", Cwbx.ParameterOutput, output.Length);
                parms["IN"].Value = codec.Encode(input, values);
                prog.Call(parms);
                var result = codec.Decode(output, (byte[])parms["OUT"].Value);
                _log.LogDebug("CALL {Lib}/{Program} rc={Rc} in {Ms} ms", lib, program, result["RC"],
                    (DateTime.UtcNow - started).TotalMilliseconds);
                return result;
            }
            finally
            {
                Cwbx.Release(parms);
                Cwbx.Release(prog);
            }
        }, ct);
    }
}

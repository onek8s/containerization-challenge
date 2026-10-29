using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudyData.Core.Design;
using StudyData.Core.Services;

namespace StudyData.Calculation;

/// <summary>
/// Finds the validated workbook of a study, checks its SHA-256 against the validation record and keeps a
/// verified local copy, so a workbook changed on the share outside change control is never used.
/// </summary>
public class WorkbookStore
{
    private readonly CalculationOptions _options;
    private readonly ILogger<WorkbookStore> _log;
    private readonly object _sync = new();

    public WorkbookStore(IOptions<CalculationOptions> options, ILogger<WorkbookStore> log)
    {
        _options = options.Value;
        _log = log;
    }

    public string SourcePath(Study study, string folder, string fileName) =>
        Path.Combine(_options.WorkbookRoot, study.Code, folder, fileName);

    public string GetVerifiedCopy(Study study, CalculationWorkbook workbook)
    {
        var cached = Path.Combine(_options.LocalCache, study.Code, $"{workbook.Code}_v{workbook.Version}_{workbook.Sha256[..12]}.xlsx");
        lock (_sync)
        {
            if (File.Exists(cached) && Hash(cached) == workbook.Sha256)
            {
                return cached;
            }

            var source = SourcePath(study, "calculations", workbook.FileName);
            if (!File.Exists(source))
            {
                throw new DerivationException($"Workbook {workbook.FileName} of {study.Code} was not found at {source}.");
            }

            var hash = Hash(source);
            if (!string.Equals(hash, workbook.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                _log.LogError("Workbook {File} of {Study} has hash {Hash}, validated hash is {Expected}", workbook.FileName, study.Code, hash, workbook.Sha256);
                throw new DerivationException($"Workbook {workbook.FileName} does not match its validation record (version {workbook.Version}).");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(cached)!);
            File.Copy(source, cached, overwrite: true);
            File.SetAttributes(cached, FileAttributes.ReadOnly);
            _log.LogInformation("Workbook {File} v{Version} of {Study} verified and cached", workbook.FileName, workbook.Version, study.Code);
            return cached;
        }
    }

    public static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}

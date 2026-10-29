using Microsoft.Extensions.Options;
using PolicyChanges.Core.Flows;
using PolicyChanges.Data.Services;

namespace PolicyChanges.DeskRunner;

/// <summary>Stores PolicyDesk screenshots on the evidence share: yyyy\MM\reference\attempt-nn-name.png.</summary>
internal sealed class EvidenceWriter
{
    private readonly string _root;
    private readonly ILogger<EvidenceWriter> _logger;

    public EvidenceWriter(IOptions<DeskOptions> options, ILogger<EvidenceWriter> logger)
    {
        _root = Environment.ExpandEnvironmentVariables(options.Value.EvidenceRoot);
        _logger = logger;
    }

    public List<EvidenceFile> Save(string reference, DateTime createdUtc, int attempt, IReadOnlyList<FlowEvidence> evidence)
    {
        var files = new List<EvidenceFile>();
        var relativeFolder = Path.Combine(createdUtc.ToString("yyyy"), createdUtc.ToString("MM"), reference);
        for (var i = 0; i < evidence.Count; i++)
        {
            var name = string.Concat(evidence[i].Name.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-'));
            var relative = Path.Combine(relativeFolder, $"{attempt:00}-{i + 1:00}-{name}.png");
            try
            {
                var full = Path.Combine(_root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllBytes(full, evidence[i].Png);
                files.Add(new EvidenceFile(evidence[i].Name, relative));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "{Reference}: could not store screenshot {Name}", reference, evidence[i].Name);
            }
        }
        return files;
    }
}

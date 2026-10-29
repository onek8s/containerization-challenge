using Microsoft.Extensions.Options;
using PolicyChanges.Core.Flows;
using PolicyChanges.Core.Requests;

namespace PolicyChanges.DeskRunner;

/// <summary>Screen flows from the flows folder, one per change type.</summary>
internal sealed class FlowLibrary
{
    private readonly Dictionary<ChangeType, FlowDefinition> _flows;

    public FlowLibrary(IOptions<DeskOptions> options, IHostEnvironment env)
        : this(Path.Combine(env.ContentRootPath, options.Value.FlowsPath))
    {
    }

    public FlowLibrary(string folder)
    {
        Folder = folder;
        _flows = Directory.EnumerateFiles(folder, "*.json")
            .Select(f => FlowDefinition.Parse(File.ReadAllText(f)))
            .ToDictionary(f => f.ChangeType);
    }

    public string Folder { get; }
    public IReadOnlyCollection<FlowDefinition> All => _flows.Values;

    public FlowDefinition? For(ChangeType type) => _flows.GetValueOrDefault(type);
}

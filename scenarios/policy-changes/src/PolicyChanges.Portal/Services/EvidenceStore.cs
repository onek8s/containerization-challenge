using Microsoft.Extensions.Options;

namespace PolicyChanges.Portal.Services;

/// <summary>Reads screenshots from the evidence share.</summary>
public sealed class EvidenceStore
{
    private readonly string _root;

    public EvidenceStore(IOptions<PortalOptions> options, IWebHostEnvironment env)
    {
        var root = options.Value.EvidenceRoot;
        _root = Path.GetFullPath(Path.IsPathRooted(root) ? root : Path.Combine(env.ContentRootPath, root));
    }

    public Stream? Open(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relativePath.Replace('\\', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) return null;
        return File.OpenRead(full);
    }
}

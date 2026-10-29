using System.Text.Json;
using Microsoft.Extensions.Options;

namespace CityPermits.Archive;

/// <summary>Development store: files in a local folder with a JSON index. No full-text search.</summary>
public sealed class FolderArchive : IDocumentArchive
{
    private readonly string _root;
    private readonly string _indexPath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FolderArchive(IOptions<ArchiveOptions> options)
    {
        _root = Path.GetFullPath(options.Value.FolderPath);
        Directory.CreateDirectory(_root);
        _indexPath = Path.Combine(_root, "index.json");
    }

    private Dictionary<Guid, string> ReadIndex() =>
        File.Exists(_indexPath) ? JsonSerializer.Deserialize<Dictionary<Guid, string>>(File.ReadAllText(_indexPath)) ?? new() : new();

    public async Task<ArchivedDocument> StoreAsync(string caseReference, string fileName, Stream content, CancellationToken ct = default)
    {
        var folder = Path.Combine(_root, ArchiveServiceCollectionExtensions.FolderName(caseReference));
        Directory.CreateDirectory(folder);
        var key = Guid.NewGuid();
        var target = Path.Combine(folder, $"{key:N}_{Path.GetFileName(fileName)}");
        long length;
        string sha;
        await using (var fs = File.Create(target))
            (length, sha) = await ArchiveServiceCollectionExtensions.CopyWithHashAsync(content, fs, ct);
        await _lock.WaitAsync(ct);
        try
        {
            var index = ReadIndex();
            index[key] = Path.GetRelativePath(_root, target);
            await File.WriteAllTextAsync(_indexPath, JsonSerializer.Serialize(index), ct);
        }
        finally { _lock.Release(); }
        return new ArchivedDocument(key, target, length, sha);
    }

    public Task<Stream> OpenReadAsync(Guid key, CancellationToken ct = default) =>
        ReadIndex().TryGetValue(key, out var rel)
            ? Task.FromResult<Stream>(File.OpenRead(Path.Combine(_root, rel)))
            : throw new FileNotFoundException($"Archive document {key} not found.");

    public Task<IReadOnlyList<ArchiveSearchHit>> SearchAsync(string text, int max = 50, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ArchiveSearchHit>>(ReadIndex()
            .Where(kv => kv.Value.Contains(text, StringComparison.OrdinalIgnoreCase))
            .Take(max).Select(kv => new ArchiveSearchHit(kv.Key, Path.GetFileName(kv.Value), kv.Value, 1)).ToList());

    public Task<string?> SharePathAsync(Guid key, CancellationToken ct = default) =>
        Task.FromResult(ReadIndex().TryGetValue(key, out var rel) ? Path.Combine(_root, rel) : null);
}

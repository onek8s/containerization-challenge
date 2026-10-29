using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain.Documents;

namespace Proseware.ControlTower.Documents.Storage;

/// <summary>Content as files below <see cref="DocumentOptions.FolderPath"/>; metadata in ct.Document.</summary>
public sealed class FolderDocumentStore : IDocumentStore
{
    private readonly ControlTowerDbContext _db;
    private readonly string _root;

    public FolderDocumentStore(ControlTowerDbContext db, IOptions<DocumentOptions> options)
    {
        _db = db;
        _root = options.Value.FolderPath;
    }

    public async Task<StoredDocument> SaveAsync(StoredDocument metadata, Stream content, CancellationToken cancellationToken = default)
    {
        if (metadata.Id == Guid.Empty) metadata.Id = Guid.NewGuid();
        if (metadata.CreatedAt == default) metadata.CreatedAt = DateTimeOffset.UtcNow;
        var path = PathFor(metadata.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        await using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            var (length, sha) = await DocumentHashing.CopyAsync(content, file, cancellationToken);
            metadata.Length = length;
            metadata.Sha256 = sha;
        }
        File.Move(temp, path, overwrite: true);

        _db.Documents.Add(metadata);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            File.Delete(path);
            throw;
        }
        return metadata;
    }

    public async Task<Stream?> OpenAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        if (!await _db.Documents.AnyAsync(d => d.Id == documentId, cancellationToken)) return null;
        var path = PathFor(documentId);
        return File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true) : null;
    }

    public async Task DeleteAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var doc = await _db.Documents.FindAsync(new object[] { documentId }, cancellationToken);
        if (doc is not null)
        {
            _db.Documents.Remove(doc);
            await _db.SaveChangesAsync(cancellationToken);
        }
        var path = PathFor(documentId);
        if (File.Exists(path)) File.Delete(path);
    }

    private string PathFor(Guid id)
    {
        var n = id.ToString("N");
        return Path.Combine(_root, n[..2], n[2..4], n + ".bin");
    }
}

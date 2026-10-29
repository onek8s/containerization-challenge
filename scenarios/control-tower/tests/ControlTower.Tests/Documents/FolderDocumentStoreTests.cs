using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Documents;
using Proseware.ControlTower.Documents.Storage;
using Proseware.ControlTower.Domain.Documents;
using Xunit;

namespace Proseware.ControlTower.Tests.Documents;

public sealed class FolderDocumentStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ct-docs-" + Guid.NewGuid().ToString("N"));
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly ControlTowerDbContext _db;
    private readonly FolderDocumentStore _store;

    public FolderDocumentStoreTests()
    {
        _connection.Open();
        _db = new ControlTowerDbContext(new DbContextOptionsBuilder<ControlTowerDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _store = new FolderDocumentStore(_db, Options.Create(new DocumentOptions { Store = "Folder", FolderPath = _folder }));
    }

    [Fact]
    public async Task Saved_content_reads_back_with_length_and_hash()
    {
        var content = new byte[200_000];
        Random.Shared.NextBytes(content);
        var saved = await _store.SaveAsync(new StoredDocument { Kind = DocumentKind.ProofOfDelivery, FileName = "pod.pdf", CreatedBy = "test" }, new MemoryStream(content));

        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.Equal(content.Length, saved.Length);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(), saved.Sha256);

        await using var stream = await _store.OpenAsync(saved.Id);
        Assert.NotNull(stream);
        using var copy = new MemoryStream();
        await stream!.CopyToAsync(copy);
        Assert.Equal(content, copy.ToArray());
        Assert.Equal(1, await _db.Documents.CountAsync());
    }

    [Fact]
    public async Task Deleted_document_is_gone()
    {
        var saved = await _store.SaveAsync(new StoredDocument { Kind = DocumentKind.Cmr, FileName = "cmr.pdf", CreatedBy = "test" }, new MemoryStream(new byte[] { 1, 2, 3 }));
        await _store.DeleteAsync(saved.Id);

        Assert.Null(await _store.OpenAsync(saved.Id));
        Assert.Equal(0, await _db.Documents.CountAsync());
        Assert.Empty(Directory.GetFiles(_folder, "*.bin", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Unknown_document_opens_as_null()
    {
        Assert.Null(await _store.OpenAsync(Guid.NewGuid()));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }
}

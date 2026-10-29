using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlTypes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CityPermits.Archive;

/// <summary>
/// Documents live in the SQL Server FileTable permits.CaseDocuments (FILESTREAM filegroup on
/// permits-sql). Files are written through the FileTable's Windows share so that the records
/// system and the scanning stations see the same folder tree; reads stream through
/// SqlFileStream inside a transaction; search uses full-text with the PDF and Office iFilters
/// installed on the SQL Server.
/// </summary>
public sealed class FileTableArchive : IDocumentArchive
{
    private readonly ArchiveOptions _options;
    private readonly ILogger<FileTableArchive> _logger;
    private string? _root;

    public FileTableArchive(IOptions<ArchiveOptions> options, ILogger<FileTableArchive> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    private SqlConnection Connect() => new(_options.ConnectionString);

    /// <summary>\\permits-sql\PERMITSFS\PermitsArchive\CaseDocuments, resolved once from the server.</summary>
    private async Task<string> RootAsync(CancellationToken ct)
    {
        if (_root is not null) return _root;
        await using var c = Connect();
        _root = await c.ExecuteScalarAsync<string>(new CommandDefinition(
            $"SELECT FileTableRootPath(N'{_options.FileTable}', 2)", cancellationToken: ct))
            ?? throw new InvalidOperationException("FILESTREAM non-transacted access is not enabled for the archive database.");
        return _root;
    }

    public async Task<ArchivedDocument> StoreAsync(string caseReference, string fileName, Stream content, CancellationToken ct = default)
    {
        var root = await RootAsync(ct);
        var folder = Path.Combine(root, ArchiveServiceCollectionExtensions.FolderName(caseReference));
        Directory.CreateDirectory(folder);
        var safe = string.Concat(fileName.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)));
        var target = Path.Combine(folder, safe);
        for (var i = 2; File.Exists(target); i++)
            target = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(safe)} ({i}){Path.GetExtension(safe)}");

        long length;
        string sha;
        await using (var fs = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            (length, sha) = await ArchiveServiceCollectionExtensions.CopyWithHashAsync(content, fs, ct);
        File.SetAttributes(target, FileAttributes.ReadOnly | FileAttributes.Archive);

        await using var c = Connect();
        var relative = target[(root.Length)..];
        var key = await c.ExecuteScalarAsync<Guid>(new CommandDefinition(
            $"SELECT stream_id FROM {_options.FileTable} WHERE file_stream.GetFileNamespacePath() = @relative",
            new { relative }, cancellationToken: ct));
        _logger.LogInformation("Archived {File} for {Case} as {Key}", safe, caseReference, key);
        return new ArchivedDocument(key, target, length, sha);
    }

    public async Task<Stream> OpenReadAsync(Guid key, CancellationToken ct = default)
    {
        var c = Connect();
        await c.OpenAsync(ct);
        var tx = c.BeginTransaction(IsolationLevel.ReadCommitted);
        var row = await c.QuerySingleOrDefaultAsync<(string Path, byte[] Context)>(new CommandDefinition(
            $"SELECT file_stream.PathName(), GET_FILESTREAM_TRANSACTION_CONTEXT() FROM {_options.FileTable} WHERE stream_id = @key",
            new { key }, tx, cancellationToken: ct));
        if (row.Path is null)
        {
            tx.Dispose();
            c.Dispose();
            throw new FileNotFoundException($"Archive document {key} not found.");
        }
        var stream = new SqlFileStream(row.Path, row.Context, FileAccess.Read, FileOptions.SequentialScan, 0);
        return new OwnedStream(stream, tx, c);
    }

    public async Task<IReadOnlyList<ArchiveSearchHit>> SearchAsync(string text, int max = 50, CancellationToken ct = default)
    {
        await using var c = Connect();
        var terms = string.Join(" AND ", text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => $"\"{t.Replace("\"", "")}*\""));
        var rows = await c.QueryAsync<ArchiveSearchHit>(new CommandDefinition($@"
SELECT TOP (@max) d.stream_id AS [Key], d.name AS Name, d.file_stream.GetFileNamespacePath() AS Path, k.[RANK] AS Rank
FROM CONTAINSTABLE({_options.FileTable}, file_stream, @terms) k
JOIN {_options.FileTable} d ON d.path_locator = k.[KEY]
ORDER BY k.[RANK] DESC", new { max, terms }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<string?> SharePathAsync(Guid key, CancellationToken ct = default)
    {
        await using var c = Connect();
        return await c.ExecuteScalarAsync<string?>(new CommandDefinition(
            $"SELECT file_stream.GetFileNamespacePath(1, 2) FROM {_options.FileTable} WHERE stream_id = @key", new { key }, cancellationToken: ct));
    }

    private sealed class OwnedStream : Stream
    {
        private readonly Stream _inner;
        private readonly SqlTransaction _tx;
        private readonly SqlConnection _connection;

        public OwnedStream(Stream inner, SqlTransaction tx, SqlConnection connection) { _inner = inner; _tx = tx; _connection = connection; }

        public override bool CanRead => true;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _tx.Commit();
                _tx.Dispose();
                _connection.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain.Documents;

namespace Proseware.ControlTower.Documents.Storage;

/// <summary>
/// Document content in the FILESTREAM table ct.DocumentContent (filegroup ControlTowerDocuments).
/// Content is streamed through the Win32 FILESTREAM API so large PDFs and scanned PODs do not go through
/// the TDS buffer; metadata row and content are written in one transaction.
/// </summary>
public sealed class SqlFileStreamDocumentStore : IDocumentStore
{
    private readonly ControlTowerDbContext _db;
    private readonly ILogger<SqlFileStreamDocumentStore> _logger;

    public SqlFileStreamDocumentStore(ControlTowerDbContext db, ILogger<SqlFileStreamDocumentStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<StoredDocument> SaveAsync(StoredDocument metadata, Stream content, CancellationToken cancellationToken = default)
    {
        if (metadata.Id == Guid.Empty) metadata.Id = Guid.NewGuid();
        if (metadata.CreatedAt == default) metadata.CreatedAt = DateTimeOffset.UtcNow;

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            var connection = (SqlConnection)_db.Database.GetDbConnection();
            var sqlTx = (SqlTransaction)tx.GetDbTransaction();

            string path;
            byte[] context;
            await using (var cmd = new SqlCommand(
                @"INSERT INTO ct.DocumentContent (Id, Content) VALUES (@id, 0x);
                  SELECT Content.PathName(), GET_FILESTREAM_TRANSACTION_CONTEXT() FROM ct.DocumentContent WHERE Id = @id;",
                connection, sqlTx))
            {
                cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = metadata.Id;
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    throw new InvalidOperationException("FILESTREAM path could not be read.");
                path = reader.GetString(0);
                context = (byte[])reader[1];
            }

            await using (var target = new SqlFileStream(path, context, FileAccess.Write, FileOptions.SequentialScan, 0))
            {
                var (length, sha) = await DocumentHashing.CopyAsync(content, target, cancellationToken);
                metadata.Length = length;
                metadata.Sha256 = sha;
            }

            _db.Documents.Add(metadata);
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        });

        _logger.LogInformation("Stored document {DocumentId} ({Kind}, {Bytes} bytes)", metadata.Id, metadata.Kind, metadata.Length);
        return metadata;
    }

    public async Task<Stream?> OpenAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_db.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        var tx = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            await using var cmd = new SqlCommand(
                "SELECT Content.PathName(), GET_FILESTREAM_TRANSACTION_CONTEXT() FROM ct.DocumentContent WHERE Id = @id",
                connection, tx);
            cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = documentId;
            string path;
            byte[] context;
            await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken))
                {
                    await tx.DisposeAsync();
                    await connection.DisposeAsync();
                    return null;
                }
                path = reader.GetString(0);
                context = (byte[])reader[1];
            }
            var stream = new SqlFileStream(path, context, FileAccess.Read, FileOptions.SequentialScan, 0);
            return new TransactionBoundStream(stream, tx, connection);
        }
        catch
        {
            await tx.DisposeAsync();
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task DeleteAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM ct.DocumentContent WHERE Id = {documentId}; DELETE FROM ct.Document WHERE Id = {documentId};",
            cancellationToken);
    }

    /// <summary>Keeps the FILESTREAM transaction open while the caller reads the content.</summary>
    private sealed class TransactionBoundStream : Stream
    {
        private readonly SqlFileStream _inner;
        private readonly SqlTransaction _tx;
        private readonly SqlConnection _connection;

        public TransactionBoundStream(SqlFileStream inner, SqlTransaction tx, SqlConnection connection)
        {
            _inner = inner;
            _tx = tx;
            _connection = connection;
        }

        public override bool CanRead => true;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                try { _tx.Commit(); } catch (InvalidOperationException) { }
                _tx.Dispose();
                _connection.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

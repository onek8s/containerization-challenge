using Dapper;

namespace Litware.DocControl.Data;

public sealed class DropFileRepository
{
    private readonly DbSession _db;

    public DropFileRepository(DbSession db) => _db = db;

    public async Task<DropFileRecord?> FindProcessedAsync(string fileId, string sha256)
    {
        await using var c = await _db.OpenAsync();
        var sql = $"SELECT {_db.Dialect.Top(1)} Id, FileId, Sha256, OriginalPath, Result, Reason, RevisionId, ProcessedUtc FROM {{s}}DropFiles "
            + $"WHERE FileId = @fileId AND Sha256 = @sha256 ORDER BY Id DESC {_db.Dialect.Limit(1)}";
        return await c.QuerySingleOrDefaultAsync<DropFileRecord>(_db.Sql(sql), new { fileId, sha256 });
    }

    public async Task AddAsync(DropFileRecord r)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecuteAsync(_db.Sql("""
            INSERT INTO {s}DropFiles (FileId, Sha256, OriginalPath, Result, Reason, RevisionId, ProcessedUtc)
            VALUES (@FileId, @Sha256, @OriginalPath, @Result, @Reason, @RevisionId, @ProcessedUtc)
            """), r);
    }

    public async Task<IReadOnlyList<DropFileRecord>> RecentAsync(int max)
    {
        await using var c = await _db.OpenAsync();
        var sql = $"SELECT {_db.Dialect.Top(max)} Id, FileId, Sha256, OriginalPath, Result, Reason, RevisionId, ProcessedUtc FROM {{s}}DropFiles ORDER BY Id DESC {_db.Dialect.Limit(max)}";
        var rows = await c.QueryAsync<DropFileRecord>(_db.Sql(sql));
        return rows.AsList();
    }
}

using Dapper;
using Litware.DocControl.Documents;

namespace Litware.DocControl.Data;

public sealed class RevisionRepository
{
    private readonly DbSession _db;

    public RevisionRepository(DbSession db) => _db = db;

    private const string Select = """
        SELECT r.Id, r.DocumentId, d.Number AS DocumentNumber, d.Title AS DocumentTitle, d.ProjectCode, r.Code, r.Status,
               r.FileName, r.VaultPath, r.Size, r.Sha256, r.Source, r.SubmittedBy, r.SubmittedUtc,
               r.ReviewedBy, r.ReviewedUtc, r.ReviewComment
        FROM {s}Revisions r
        JOIN {s}Documents d ON d.Id = r.DocumentId
        """;

    public async Task<IReadOnlyList<RevisionRecord>> ForDocumentAsync(int documentId)
    {
        await using var c = await _db.OpenAsync();
        var rows = await c.QueryAsync<RevisionRecord>(_db.Sql(Select + " WHERE r.DocumentId = @documentId ORDER BY r.Id DESC"), new { documentId });
        return rows.AsList();
    }

    public async Task<RevisionRecord?> GetAsync(int id)
    {
        await using var c = await _db.OpenAsync();
        return await c.QuerySingleOrDefaultAsync<RevisionRecord>(_db.Sql(Select + " WHERE r.Id = @id"), new { id });
    }

    public async Task<IReadOnlyList<RevisionRecord>> GetManyAsync(IEnumerable<int> ids)
    {
        var list = ids.Distinct().ToArray();
        if (list.Length == 0) return Array.Empty<RevisionRecord>();
        await using var c = await _db.OpenAsync();
        var rows = await c.QueryAsync<RevisionRecord>(_db.Sql(Select + " WHERE r.Id IN @list ORDER BY d.Number"), new { list });
        return rows.AsList();
    }

    public async Task<IReadOnlyList<RevisionRecord>> PendingReviewAsync(string? projectCode = null)
    {
        await using var c = await _db.OpenAsync();
        var sql = Select + " WHERE r.Status = 0" + (projectCode is null ? "" : " AND d.ProjectCode = @projectCode") + " ORDER BY r.SubmittedUtc";
        var rows = await c.QueryAsync<RevisionRecord>(_db.Sql(sql), new { projectCode });
        return rows.AsList();
    }

    /// <summary>The revision to put on a transmittal: approved revision, or the submitted one for IFR.</summary>
    public async Task<IReadOnlyList<RevisionRecord>> IssuableAsync(string projectCode, bool includeSubmitted)
    {
        await using var c = await _db.OpenAsync();
        var sql = Select + " WHERE d.ProjectCode = @projectCode AND (r.Status = 1" + (includeSubmitted ? " OR r.Status = 0" : "") + ") ORDER BY d.Number, r.Id";
        var rows = await c.QueryAsync<RevisionRecord>(_db.Sql(sql), new { projectCode });
        return rows.AsList();
    }

    public async Task<int> AddAsync(RevisionRecord r)
    {
        await using var c = await _db.OpenAsync();
        var sql = _db.Dialect.InsertReturningId(_db.Sql("""
            INSERT INTO {s}Revisions (DocumentId, Code, Status, FileName, VaultPath, Size, Sha256, Source, SubmittedBy, SubmittedUtc)
            VALUES (@DocumentId, @Code, @Status, @FileName, @VaultPath, @Size, @Sha256, @Source, @SubmittedBy, @SubmittedUtc)
            """));
        return await c.ExecuteScalarAsync<int>(sql, new
        {
            r.DocumentId,
            r.Code,
            Status = (int)r.Status,
            r.FileName,
            r.VaultPath,
            r.Size,
            r.Sha256,
            r.Source,
            r.SubmittedBy,
            r.SubmittedUtc,
        });
    }

    /// <summary>Approves the revision and supersedes the previously approved one, in one transaction.</summary>
    public async Task<bool> ApproveAsync(int id, string reviewer, string? comment)
    {
        await using var c = await _db.OpenAsync();
        await using var tx = await c.BeginTransactionAsync();
        var docId = await c.ExecuteScalarAsync<int?>(_db.Sql("SELECT DocumentId FROM {s}Revisions WHERE Id = @id AND Status = 0"), new { id }, tx);
        if (docId is null) return false;

        await c.ExecuteAsync(_db.Sql("UPDATE {s}Revisions SET Status = 3 WHERE DocumentId = @docId AND Status = 1"), new { docId }, tx);
        await c.ExecuteAsync(_db.Sql("UPDATE {s}Revisions SET Status = 1, ReviewedBy = @reviewer, ReviewedUtc = @now, ReviewComment = @comment WHERE Id = @id"),
            new { id, reviewer, now = DateTime.UtcNow, comment }, tx);
        await tx.CommitAsync();
        return true;
    }

    public async Task<bool> RejectAsync(int id, string reviewer, string comment)
    {
        await using var c = await _db.OpenAsync();
        var n = await c.ExecuteAsync(_db.Sql("UPDATE {s}Revisions SET Status = 2, ReviewedBy = @reviewer, ReviewedUtc = @now, ReviewComment = @comment WHERE Id = @id AND Status = 0"),
            new { id, reviewer, now = DateTime.UtcNow, comment });
        return n == 1;
    }

    public async Task<IReadOnlyList<(RevisionCode Code, RevisionStatus Status)>> HistoryAsync(int documentId)
    {
        await using var c = await _db.OpenAsync();
        var rows = await c.QueryAsync<(string Code, int Status)>(_db.Sql("SELECT Code, Status FROM {s}Revisions WHERE DocumentId = @documentId"), new { documentId });
        return rows.Select(r => (RevisionCode.Parse(r.Code), (RevisionStatus)r.Status)).ToList();
    }

    public async Task<bool> HashExistsAsync(int documentId, string sha256)
    {
        await using var c = await _db.OpenAsync();
        return await c.ExecuteScalarAsync<int>(_db.Sql("SELECT COUNT(*) FROM {s}Revisions WHERE DocumentId = @documentId AND Sha256 = @sha256 AND Status <> 2"),
            new { documentId, sha256 }) > 0;
    }
}

using Dapper;
using Litware.DocControl.Documents;

namespace Litware.DocControl.Data;

public sealed class DocumentRepository
{
    private readonly DbSession _db;

    public DocumentRepository(DbSession db) => _db = db;

    public async Task<IReadOnlyList<Project>> ProjectsAsync(bool activeOnly = true)
    {
        await using var c = await _db.OpenAsync();
        var rows = await c.QueryAsync<Project>(_db.Sql(
            "SELECT Code, Name, Client, Active FROM {s}Projects" + (activeOnly ? " WHERE Active = 1" : "") + " ORDER BY Code"));
        return rows.AsList();
    }

    public async Task<Project?> ProjectAsync(string code)
    {
        await using var c = await _db.OpenAsync();
        return await c.QuerySingleOrDefaultAsync<Project>(_db.Sql("SELECT Code, Name, Client, Active FROM {s}Projects WHERE Code = @code"), new { code });
    }

    private const string RegisterSelect = """
        SELECT d.Id, d.Number, d.ProjectCode, d.Discipline, d.DocumentType, d.Title, d.CreatedUtc, d.CreatedBy,
               lr.Code AS LatestRevision, lr.Status AS LatestStatus,
               (SELECT ar.Code FROM {s}Revisions ar WHERE ar.DocumentId = d.Id AND ar.Status = 1) AS ApprovedRevision
        FROM {s}Documents d
        LEFT JOIN {s}Revisions lr ON lr.Id = (SELECT MAX(r.Id) FROM {s}Revisions r WHERE r.DocumentId = d.Id)
        """;

    public async Task<IReadOnlyList<DocumentRecord>> RegisterAsync(string projectCode, string? discipline = null, string? search = null)
    {
        await using var c = await _db.OpenAsync();
        var sql = RegisterSelect + " WHERE d.ProjectCode = @projectCode";
        if (!string.IsNullOrWhiteSpace(discipline)) sql += " AND d.Discipline = @discipline";
        if (!string.IsNullOrWhiteSpace(search)) sql += " AND (d.Number LIKE @like OR d.Title LIKE @like)";
        sql += " ORDER BY d.Number";
        var rows = await c.QueryAsync<DocumentRecord>(_db.Sql(sql),
            new { projectCode, discipline, like = "%" + search?.Trim() + "%" }, commandTimeout: _db.CommandTimeout);
        return rows.AsList();
    }

    public async Task<DocumentRecord?> FindAsync(string number)
    {
        await using var c = await _db.OpenAsync();
        return await c.QuerySingleOrDefaultAsync<DocumentRecord>(_db.Sql(RegisterSelect + " WHERE d.Number = @number"), new { number });
    }

    public async Task<int> CreateAsync(DocumentNumber number, string title, string createdBy)
    {
        await using var c = await _db.OpenAsync();
        var sql = _db.Dialect.InsertReturningId(_db.Sql("""
            INSERT INTO {s}Documents (Number, ProjectCode, Discipline, DocumentType, Title, CreatedUtc, CreatedBy)
            VALUES (@Number, @Project, @Discipline, @DocumentType, @Title, @Now, @CreatedBy)
            """));
        return await c.ExecuteScalarAsync<int>(sql, new
        {
            Number = number.ToString(),
            number.Project,
            number.Discipline,
            number.DocumentType,
            Title = title,
            Now = DateTime.UtcNow,
            CreatedBy = createdBy,
        });
    }

    public async Task UpdateTitleAsync(int id, string title)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecuteAsync(_db.Sql("UPDATE {s}Documents SET Title = @title WHERE Id = @id"), new { id, title });
    }
}

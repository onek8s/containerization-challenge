using Dapper;
using Litware.DocControl.Transmittals;

namespace Litware.DocControl.Data;

public sealed class TransmittalRepository
{
    private readonly DbSession _db;

    public TransmittalRepository(DbSession db) => _db = db;

    private const string Select = """
        SELECT t.Id, t.Number, t.ProjectCode, t.Purpose, t.RecipientCompany, t.RecipientName, t.RecipientEmail, t.Notes,
               t.Status, t.CreatedBy, t.CreatedByEmail, t.CreatedUtc, t.IssuedUtc, t.PackagePath, t.PackageSize, t.Error,
               t.Attempts, t.ClaimedBy, t.ClaimedUtc,
               (SELECT COUNT(*) FROM {s}TransmittalItems i WHERE i.TransmittalId = t.Id) AS ItemCount
        FROM {s}Transmittals t
        """;

    public async Task<IReadOnlyList<TransmittalRecord>> ListAsync(string? projectCode, int max = 200)
    {
        await using var c = await _db.OpenAsync();
        var sql = $"SELECT {_db.Dialect.Top(max)} * FROM ({Select}) x"
            + (projectCode is null ? "" : " WHERE x.ProjectCode = @projectCode")
            + $" ORDER BY x.Id DESC {_db.Dialect.Limit(max)}";
        var rows = await c.QueryAsync<TransmittalRecord>(_db.Sql(sql), new { projectCode });
        return rows.AsList();
    }

    public async Task<TransmittalRecord?> GetAsync(int id)
    {
        await using var c = await _db.OpenAsync();
        return await c.QuerySingleOrDefaultAsync<TransmittalRecord>(_db.Sql(Select + " WHERE t.Id = @id"), new { id });
    }

    public async Task<TransmittalRecord?> FindAsync(string number)
    {
        await using var c = await _db.OpenAsync();
        return await c.QuerySingleOrDefaultAsync<TransmittalRecord>(_db.Sql(Select + " WHERE t.Number = @number"), new { number });
    }

    public async Task<IReadOnlyList<int>> ItemsAsync(int transmittalId)
    {
        await using var c = await _db.OpenAsync();
        var rows = await c.QueryAsync<int>(_db.Sql("SELECT RevisionId FROM {s}TransmittalItems WHERE TransmittalId = @transmittalId"), new { transmittalId });
        return rows.AsList();
    }

    /// <summary>Creates a queued transmittal with the next number for the project.</summary>
    public async Task<TransmittalRecord> CreateAsync(TransmittalRecord t, IReadOnlyCollection<int> revisionIds)
    {
        await using var c = await _db.OpenAsync();
        await using var tx = await c.BeginTransactionAsync();

        var seq = await c.ExecuteScalarAsync<int>(_db.Sql(_db.Dialect.NextCounterValue), new { Name = TransmittalNumber.CounterName(t.ProjectCode) }, tx);
        t.Number = TransmittalNumber.Format(t.ProjectCode, seq);
        t.Status = TransmittalStatus.Queued;
        t.CreatedUtc = DateTime.UtcNow;

        t.Id = await c.ExecuteScalarAsync<int>(_db.Dialect.InsertReturningId(_db.Sql("""
            INSERT INTO {s}Transmittals (Number, ProjectCode, Purpose, RecipientCompany, RecipientName, RecipientEmail, Notes, Status, CreatedBy, CreatedByEmail, CreatedUtc, Attempts)
            VALUES (@Number, @ProjectCode, @Purpose, @RecipientCompany, @RecipientName, @RecipientEmail, @Notes, 0, @CreatedBy, @CreatedByEmail, @CreatedUtc, 0)
            """)), new
        {
            t.Number,
            t.ProjectCode,
            Purpose = (int)t.Purpose,
            t.RecipientCompany,
            t.RecipientName,
            t.RecipientEmail,
            t.Notes,
            t.CreatedBy,
            t.CreatedByEmail,
            t.CreatedUtc,
        }, tx);

        foreach (var rev in revisionIds)
            await c.ExecuteAsync(_db.Sql("INSERT INTO {s}TransmittalItems (TransmittalId, RevisionId) VALUES (@Id, @rev)"), new { t.Id, rev }, tx);

        await tx.CommitAsync();
        t.ItemCount = revisionIds.Count;
        return t;
    }

    /// <summary>
    /// Claims the next queued transmittal (or one whose build was abandoned before <paramref name="staleBefore"/>).
    /// </summary>
    public async Task<TransmittalRecord?> ClaimNextAsync(string worker, DateTime staleBefore, CancellationToken cancellationToken)
    {
        await using var c = await _db.OpenAsync(cancellationToken);
        var id = await c.ExecuteScalarAsync<int?>(new CommandDefinition(_db.Sql(_db.Dialect.ClaimTransmittal),
            new { Worker = worker, Now = DateTime.UtcNow, StaleBefore = staleBefore }, cancellationToken: cancellationToken));
        return id is null ? null : await GetAsync(id.Value);
    }

    public async Task MarkIssuedAsync(int id, string packagePath, long packageSize)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecuteAsync(_db.Sql("UPDATE {s}Transmittals SET Status = 2, IssuedUtc = @now, PackagePath = @packagePath, PackageSize = @packageSize, Error = NULL WHERE Id = @id"),
            new { id, now = DateTime.UtcNow, packagePath, packageSize });
    }

    public async Task MarkFailedAsync(int id, string error, bool retry)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecuteAsync(_db.Sql("UPDATE {s}Transmittals SET Status = @status, Error = @error, ClaimedBy = NULL WHERE Id = @id"),
            new { id, status = retry ? (int)TransmittalStatus.Queued : (int)TransmittalStatus.Failed, error });
    }

    public async Task<bool> RequeueAsync(int id)
    {
        await using var c = await _db.OpenAsync();
        return await c.ExecuteAsync(_db.Sql("UPDATE {s}Transmittals SET Status = 0, Attempts = 0, Error = NULL WHERE Id = @id AND Status = 3"), new { id }) == 1;
    }

    public async Task<bool> CancelAsync(int id)
    {
        await using var c = await _db.OpenAsync();
        return await c.ExecuteAsync(_db.Sql("UPDATE {s}Transmittals SET Status = 4 WHERE Id = @id AND Status IN (0, 3)"), new { id }) == 1;
    }
}

using System.Data;

namespace Lucerne.Lending.Data;

public sealed class LedgerRepository
{
    private readonly Db _db;

    public LedgerRepository(Db db) => _db = db;

    public Task AddAsync(IEnumerable<GlEntryRow> entries, IDbTransaction? tx = null) =>
        _db.ExecuteAsync(@"INSERT INTO lending.GlEntry(BusinessDate, GlAccount, CostCenter, Debit, Credit, Text, SourceType, SourceRef, CreatedUtc)
            VALUES (@BusinessDate, @GlAccount, @CostCenter, @Debit, @Credit, @Text, @SourceType, @SourceRef, @CreatedUtc)",
            entries.Select(e => { e.CreatedUtc = DateTime.UtcNow; return e; }).ToList(), tx);

    public Task<IReadOnlyList<GlEntryRow>> UnexportedAsync(DateOnly upTo, CancellationToken ct = default) =>
        _db.QueryAsync<GlEntryRow>("SELECT * FROM lending.GlEntry WHERE ExportBatch IS NULL AND BusinessDate <= @upTo ORDER BY BusinessDate, Id",
            new { upTo }, ct: ct);

    public Task MarkExportedAsync(string batch, IEnumerable<long> ids) =>
        _db.ExecuteAsync("UPDATE lending.GlEntry SET ExportBatch = @batch WHERE Id IN @ids", new { batch, ids = ids.ToArray() });

    public Task<bool> ExistsAsync(string sourceType, string sourceRef) =>
        _db.ScalarAsync<int>("SELECT COUNT(*) FROM lending.GlEntry WHERE SourceType = @sourceType AND SourceRef = @sourceRef",
            new { sourceType, sourceRef }).ContinueWith(t => t.Result > 0);
}

public sealed class JobRunRepository
{
    private readonly Db _db;

    public JobRunRepository(Db db) => _db = db;

    public Task<long> StartAsync(string job) =>
        _db.ScalarAsync<long>(@"INSERT INTO lending.JobRun(Job, Host, StartedUtc, Status) VALUES (@job, @host, @now, 'Running');
            SELECT CAST(SCOPE_IDENTITY() AS bigint);", new { job, host = Environment.MachineName, now = DateTime.UtcNow });

    public Task FinishAsync(long id, string status, string? detail) =>
        _db.ExecuteAsync("UPDATE lending.JobRun SET FinishedUtc = @now, Status = @status, Detail = @detail WHERE Id = @id",
            new { id, status, detail = detail?.Length > 2000 ? detail[..2000] : detail, now = DateTime.UtcNow });

    public Task<JobRunRow?> LastSuccessAsync(string job) =>
        _db.FirstOrDefaultAsync<JobRunRow>("SELECT TOP (1) * FROM lending.JobRun WHERE Job = @job AND Status = 'Succeeded' ORDER BY Id DESC", new { job });

    public Task<IReadOnlyList<JobRunRow>> RecentAsync(int take) =>
        _db.QueryAsync<JobRunRow>("SELECT TOP (@take) * FROM lending.JobRun ORDER BY Id DESC", new { take });
}

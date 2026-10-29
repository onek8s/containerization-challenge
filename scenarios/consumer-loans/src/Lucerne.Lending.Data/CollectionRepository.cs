using System.Data;
using Lucerne.Lending.Domain;

namespace Lucerne.Lending.Data;

public sealed class CollectionRepository
{
    private readonly Db _db;

    public CollectionRepository(Db db) => _db = db;

    public Task<CollectionCaseRow?> OpenCaseAsync(string accountNumber, CancellationToken ct = default) =>
        _db.FirstOrDefaultAsync<CollectionCaseRow>("SELECT TOP (1) * FROM lending.CollectionCase WHERE AccountNumber = @accountNumber AND Status <> 9 ORDER BY Id DESC",
            new { accountNumber }, ct: ct);

    public Task<CollectionCaseRow?> GetAsync(long id, CancellationToken ct = default) =>
        _db.FirstOrDefaultAsync<CollectionCaseRow>("SELECT * FROM lending.CollectionCase WHERE Id = @id", new { id }, ct: ct);

    public Task<CollectionCaseRow?> GetAsync(string reference, CancellationToken ct = default) =>
        _db.FirstOrDefaultAsync<CollectionCaseRow>("SELECT * FROM lending.CollectionCase WHERE Reference = @reference", new { reference }, ct: ct);

    public async Task<CollectionCaseRow> CreateAsync(string accountNumber, string customerNumber, decimal arrears, int dpd, CancellationToken ct = default)
    {
        return await _db.InTransactionAsync(async tx =>
        {
            var reference = ReferenceGenerator.CollectionCase(await _db.NextSequenceAsync("collection", tx));
            var id = await _db.ScalarAsync<long>(@"INSERT INTO lending.CollectionCase(Reference, AccountNumber, CustomerNumber, Stage, Status, Arrears,
                    DaysPastDue, FeesCharged, OpenedUtc, Version)
                VALUES (@reference, @accountNumber, @customerNumber, 0, 1, @arrears, @dpd, 0, @now, 1);
                SELECT CAST(SCOPE_IDENTITY() AS bigint);",
                new { reference, accountNumber, customerNumber, arrears, dpd, now = DateTime.UtcNow }, tx);
            await AddActionAsync(id, "Opened", $"Arrears {arrears:0.00} EUR, {dpd} days past due", "SYSTEM", tx);
            return (await _db.FirstOrDefaultAsync<CollectionCaseRow>("SELECT * FROM lending.CollectionCase WHERE Id = @id", new { id }, tx))!;
        }, ct);
    }

    public Task UpdateArrearsAsync(long id, decimal arrears, int dpd, IDbTransaction? tx = null) =>
        _db.ExecuteAsync("UPDATE lending.CollectionCase SET Arrears = @arrears, DaysPastDue = @dpd, Version = Version + 1 WHERE Id = @id",
            new { id, arrears, dpd }, tx);

    public async Task<bool> MoveStageAsync(long id, CollectionStage stage, bool feeCharged, int version, IDbTransaction? tx = null)
    {
        var rows = await _db.ExecuteAsync(@"UPDATE lending.CollectionCase SET Stage = @stage, FeesCharged = FeesCharged + @fee,
                LastActionUtc = @now, Version = Version + 1,
                Status = CASE WHEN @stage = 5 THEN 4 ELSE Status END
            WHERE Id = @id AND Version = @version",
            new { id, stage = (int)stage, fee = feeCharged ? 1 : 0, now = DateTime.UtcNow, version }, tx);
        return rows == 1;
    }

    public Task SetStatusAsync(long id, CollectionStatus status, IDbTransaction? tx = null) =>
        _db.ExecuteAsync(@"UPDATE lending.CollectionCase SET Status = @status, Version = Version + 1, LastActionUtc = @now,
                ClosedUtc = CASE WHEN @status = 9 THEN @now ELSE ClosedUtc END WHERE Id = @id",
            new { id, status = (int)status, now = DateTime.UtcNow }, tx);

    public Task SetPromiseAsync(long id, DateOnly date, decimal amount, IDbTransaction? tx = null) =>
        _db.ExecuteAsync("UPDATE lending.CollectionCase SET PromiseDate = @date, PromiseAmount = @amount, Status = 3, Version = Version + 1 WHERE Id = @id",
            new { id, date, amount }, tx);

    public Task AssignAsync(long id, string? user) =>
        _db.ExecuteAsync("UPDATE lending.CollectionCase SET AssignedTo = @user WHERE Id = @id", new { id, user });

    public Task AddActionAsync(long caseId, string type, string? detail, string actor, IDbTransaction? tx = null) =>
        _db.ExecuteAsync(@"INSERT INTO lending.CollectionAction(CaseId, ActionType, Detail, Actor, CreatedUtc)
            VALUES (@caseId, @type, @detail, @actor, @now)", new { caseId, type, detail, actor, now = DateTime.UtcNow }, tx);

    public Task<IReadOnlyList<CollectionActionRow>> ActionsAsync(long caseId, CancellationToken ct = default) =>
        _db.QueryAsync<CollectionActionRow>("SELECT * FROM lending.CollectionAction WHERE CaseId = @caseId ORDER BY Id DESC", new { caseId }, ct: ct);

    public Task<IReadOnlyList<CollectionCaseRow>> ListAsync(CollectionStatus? status, int? minStage, CancellationToken ct = default)
    {
        var sql = "SELECT * FROM lending.CollectionCase WHERE 1 = 1";
        if (status.HasValue) sql += " AND Status = @status";
        else sql += " AND Status <> 9";
        if (minStage.HasValue) sql += " AND Stage >= @minStage";
        return _db.QueryAsync<CollectionCaseRow>(sql + " ORDER BY DaysPastDue DESC, Id", new { status = (int?)status, minStage }, ct: ct);
    }

    public Task<IReadOnlyList<CollectionCaseRow>> OpenCasesAsync(CancellationToken ct = default) =>
        _db.QueryAsync<CollectionCaseRow>("SELECT * FROM lending.CollectionCase WHERE Status IN (1, 2, 3) ORDER BY Id", ct: ct);

    public Task AddPlanAsync(PaymentPlanRow plan, IDbTransaction? tx = null) =>
        _db.ExecuteAsync(@"INSERT INTO lending.PaymentPlan(CaseId, AccountNumber, Months, InstalmentsJson, Status, CreatedBy, CreatedUtc)
            VALUES (@CaseId, @AccountNumber, @Months, @InstalmentsJson, @Status, @CreatedBy, @CreatedUtc)", plan, tx);

    public Task<IReadOnlyList<PaymentPlanRow>> PlansAsync(long caseId, CancellationToken ct = default) =>
        _db.QueryAsync<PaymentPlanRow>("SELECT * FROM lending.PaymentPlan WHERE CaseId = @caseId ORDER BY Id DESC", new { caseId }, ct: ct);

    public Task<IReadOnlyList<PaymentPlanRow>> ActivePlansAsync(CancellationToken ct = default) =>
        _db.QueryAsync<PaymentPlanRow>("SELECT * FROM lending.PaymentPlan WHERE Status = 'Active'", ct: ct);

    public Task SetPlanStatusAsync(long planId, string status) =>
        _db.ExecuteAsync("UPDATE lending.PaymentPlan SET Status = @status WHERE Id = @planId", new { planId, status });
}

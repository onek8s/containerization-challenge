using System.Data;
using ContosoFoods.TradeRebates.Core;
using Dapper;

namespace ContosoFoods.TradeRebates.Data;

public sealed class SqlRebateStore(RebateDatabase db) : IPortalStore, IDistributorApiStore
{
    private const CommandType Proc = CommandType.StoredProcedure;
    private int Timeout => db.CommandTimeoutSeconds;

    public async Task<Dashboard> GetDashboardAsync()
    {
        await using var c = await db.OpenAsync();
        using var r = await c.QueryMultipleAsync("rebate.Dashboard", commandType: Proc, commandTimeout: Timeout);
        return new Dashboard
        {
            Summary = await r.ReadSingleAsync<DashboardSummary>(),
            NearNextTier = (await r.ReadAsync<TierProgress>()).ToList(),
            Rates = (await r.ReadAsync<DistributorRate>()).ToList()
        };
    }

    public async Task<IReadOnlyList<Distributor>> ListDistributorsAsync()
    {
        await using var c = await db.OpenAsync();
        return (await c.QueryAsync<Distributor>("rebate.ListDistributors", commandType: Proc, commandTimeout: Timeout)).ToList();
    }

    public Task SaveBankAccountAsync(int distributorId, string iban, string actor) => RebateDatabase.Rules(async () =>
    {
        await using var c = await db.OpenAsync();
        await c.ExecuteAsync("rebate.SaveDistributorBankAccount", new { DistributorId = distributorId, Iban = iban, Actor = actor },
            commandType: Proc, commandTimeout: Timeout);
    });

    public async Task<string?> RevealBankAccountAsync(int distributorId, string actor)
    {
        await using var c = await db.OpenAsync();
        return await c.ExecuteScalarAsync<string?>("rebate.RevealDistributorBankAccount", new { DistributorId = distributorId, Actor = actor },
            commandType: Proc, commandTimeout: Timeout);
    }

    public async Task<IReadOnlyList<AgreementRow>> ListAgreementsAsync(int? distributorId, string? status)
    {
        await using var c = await db.OpenAsync();
        return (await c.QueryAsync<AgreementRow>("rebate.ListAgreements", new { DistributorId = distributorId, Status = status },
            commandType: Proc, commandTimeout: Timeout)).ToList();
    }

    public async Task<(AgreementDetail? Agreement, IReadOnlyList<AccrualRow> Accruals)> GetAgreementAsync(int agreementId)
    {
        await using var c = await db.OpenAsync();
        using var r = await c.QueryMultipleAsync("rebate.GetAgreement", new { AgreementId = agreementId }, commandType: Proc, commandTimeout: Timeout);
        var agreement = await r.ReadSingleOrDefaultAsync<AgreementDetail>();
        var accruals = (await r.ReadAsync<AccrualRow>()).ToList();
        return (agreement, accruals);
    }

    public Task<int> SaveAgreementAsync(AgreementInput input, string actor) => RebateDatabase.Rules(async () =>
    {
        await using var c = await db.OpenAsync();
        var p = new DynamicParameters();
        p.Add("AgreementId", input.AgreementId);
        p.Add("AgreementNo", input.AgreementNo);
        p.Add("DistributorId", input.DistributorId);
        p.Add("Description", input.Description);
        p.Add("Basis", input.Basis, DbType.AnsiStringFixedLength, size: 1);
        p.Add("Category", input.Category);
        p.Add("Tiers", input.Tiers);
        p.Add("ValidFrom", input.ValidFrom.Date, DbType.Date);
        p.Add("ValidTo", input.ValidTo.Date, DbType.Date);
        p.Add("CapAmount", input.CapAmount);
        p.Add("RowVer", input.RowVer, DbType.Binary, size: 8);
        p.Add("Actor", actor);
        p.Add("NewAgreementId", dbType: DbType.Int32, direction: ParameterDirection.Output);
        await c.ExecuteAsync("rebate.SaveAgreement", p, commandType: Proc, commandTimeout: Timeout);
        return p.Get<int>("NewAgreementId");
    });

    public Task SetAgreementStatusAsync(int agreementId, string status, string actor) => RebateDatabase.Rules(async () =>
    {
        await using var c = await db.OpenAsync();
        await c.ExecuteAsync("rebate.SetAgreementStatus", new { AgreementId = agreementId, Status = status, Actor = actor },
            commandType: Proc, commandTimeout: Timeout);
    });

    public async Task<IReadOnlyList<ClaimRow>> ListClaimsAsync(string? status, int? distributorId, int? period)
    {
        await using var c = await db.OpenAsync();
        return (await c.QueryAsync<ClaimRow>("rebate.ListClaims", new { Status = status, DistributorId = distributorId, Period = period },
            commandType: Proc, commandTimeout: Timeout)).ToList();
    }

    public async Task<ClaimDetail?> GetClaimAsync(int claimId)
    {
        await using var c = await db.OpenAsync();
        using var r = await c.QueryMultipleAsync("rebate.GetClaim", new { ClaimId = claimId }, commandType: Proc, commandTimeout: Timeout);
        var header = await r.ReadSingleOrDefaultAsync<ClaimHeader>();
        if (header is null)
            return null;
        return new ClaimDetail
        {
            Header = header,
            Lines = (await r.ReadAsync<ClaimLineRow>()).ToList(),
            Issues = (await r.ReadAsync<ClaimIssueRow>()).ToList()
        };
    }

    public Task ValidateClaimAsync(int claimId) => RebateDatabase.Rules(async () =>
    {
        await using var c = await db.OpenAsync();
        await c.ExecuteAsync("rebate.ValidateClaim", new { ClaimId = claimId }, commandType: Proc, commandTimeout: Timeout);
    });

    public Task DecideClaimAsync(int claimId, byte[] rowVer, bool approve, string actor, string? note) => RebateDatabase.Rules(async () =>
    {
        await using var c = await db.OpenAsync();
        await c.ExecuteAsync("rebate.DecideClaim", new { ClaimId = claimId, RowVer = rowVer, Approve = approve, Actor = actor, Note = note },
            commandType: Proc, commandTimeout: Timeout);
    });

    public Task<IReadOnlyList<WorkbookImportResult>> ImportWorkbookAsync(string path, string actor) => RebateDatabase.Rules(async () =>
    {
        await using var c = await db.OpenAsync();
        IReadOnlyList<WorkbookImportResult> rows = (await c.QueryAsync<WorkbookImportResult>("rebate.ImportSellOutWorkbook",
            new { Path = path, SubmittedBy = actor }, commandType: Proc, commandTimeout: Timeout * 5)).ToList();
        return rows;
    });

    public Task CalculateAccrualsAsync(int period) => RebateDatabase.Rules(async () =>
    {
        await using var c = await db.OpenAsync();
        await c.ExecuteAsync("rebate.CalculateAccruals", new { Period = period }, commandType: Proc, commandTimeout: Timeout * 5);
    });

    public Task<IReadOnlyList<RequestedStatement>> RequestStatementsAsync(string quarter, string actor, int? distributorId) => RebateDatabase.Rules(async () =>
    {
        await using var c = await db.OpenAsync();
        IReadOnlyList<RequestedStatement> rows = (await c.QueryAsync<RequestedStatement>("rebate.RequestStatements",
            new { Quarter = quarter, RequestedBy = actor, DistributorId = distributorId }, commandType: Proc, commandTimeout: Timeout)).ToList();
        return rows;
    });

    public async Task<IReadOnlyList<StatementRow>> ListStatementsAsync(string? quarter)
    {
        await using var c = await db.OpenAsync();
        return (await c.QueryAsync<StatementRow>("rebate.ListStatements", new { Quarter = quarter }, commandType: Proc, commandTimeout: Timeout)).ToList();
    }

    public async Task<StatementDocument?> GetStatementAsync(int statementId)
    {
        await using var c = await db.OpenAsync();
        return await StatementQueries.GetAsync(c, null, statementId, Timeout);
    }

    public async Task<IReadOnlyList<AuditRow>> ListAuditAsync(string? entity, string? entityId)
    {
        await using var c = await db.OpenAsync();
        return (await c.QueryAsync<AuditRow>("rebate.ListAudit", new { Entity = entity, EntityId = entityId }, commandType: Proc, commandTimeout: Timeout)).ToList();
    }

    public async Task<string?> GetSettingAsync(string name)
    {
        await using var c = await db.OpenAsync();
        return await c.ExecuteScalarAsync<string?>("SELECT rebate.GetSetting(@name);", new { name }, commandTimeout: Timeout);
    }

    // Distributor API

    public async Task<ApiClientInfo?> ResolveApiClientAsync(byte[] keyHash)
    {
        await using var c = await db.OpenAsync();
        return await c.QuerySingleOrDefaultAsync<ApiClientInfo>("rebate.ResolveApiClient", new { KeyHash = keyHash },
            commandType: Proc, commandTimeout: Timeout);
    }

    public Task<int> SubmitClaimAsync(int distributorId, int period, string? reference, string submittedBy, IReadOnlyList<ClaimLineInput> lines) =>
        RebateDatabase.Rules(async () =>
        {
            await using var c = await db.OpenAsync();
            var p = new DynamicParameters();
            p.Add("DistributorId", distributorId);
            p.Add("Period", period);
            p.Add("Source", "API", DbType.AnsiString, size: 10);
            p.Add("SourceRef", reference);
            p.Add("SubmittedBy", submittedBy);
            p.Add("Lines", RebateDatabase.ClaimLineTable(lines).AsTableValuedParameter("rebate.ClaimLineTable"));
            p.Add("ClaimId", dbType: DbType.Int32, direction: ParameterDirection.Output);
            await c.ExecuteAsync("rebate.SubmitClaim", p, commandType: Proc, commandTimeout: Timeout);
            return p.Get<int>("ClaimId");
        });

    public async Task<IReadOnlyList<DistributorClaim>> ListClaimsAsync(int distributorId, int? period)
    {
        await using var c = await db.OpenAsync();
        return (await c.QueryAsync<DistributorClaim>("rebate.ListDistributorClaims", new { DistributorId = distributorId, Period = period },
            commandType: Proc, commandTimeout: Timeout)).ToList();
    }

    public async Task<IReadOnlyList<DistributorAgreement>> ListAgreementsAsync(int distributorId)
    {
        await using var c = await db.OpenAsync();
        return (await c.QueryAsync<DistributorAgreement>("rebate.ListDistributorAgreements", new { DistributorId = distributorId },
            commandType: Proc, commandTimeout: Timeout)).ToList();
    }
}

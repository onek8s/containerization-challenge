using ContosoFoods.TradeRebates.Core;

namespace ContosoFoods.TradeRebates.Data;

public interface IPortalStore
{
    Task<Dashboard> GetDashboardAsync();
    Task<IReadOnlyList<Distributor>> ListDistributorsAsync();
    Task SaveBankAccountAsync(int distributorId, string iban, string actor);
    Task<string?> RevealBankAccountAsync(int distributorId, string actor);
    Task<IReadOnlyList<AgreementRow>> ListAgreementsAsync(int? distributorId, string? status);
    Task<(AgreementDetail? Agreement, IReadOnlyList<AccrualRow> Accruals)> GetAgreementAsync(int agreementId);
    Task<int> SaveAgreementAsync(AgreementInput input, string actor);
    Task SetAgreementStatusAsync(int agreementId, string status, string actor);
    Task<IReadOnlyList<ClaimRow>> ListClaimsAsync(string? status, int? distributorId, int? period);
    Task<ClaimDetail?> GetClaimAsync(int claimId);
    Task ValidateClaimAsync(int claimId);
    Task DecideClaimAsync(int claimId, byte[] rowVer, bool approve, string actor, string? note);
    Task<IReadOnlyList<WorkbookImportResult>> ImportWorkbookAsync(string path, string actor);
    Task CalculateAccrualsAsync(int period);
    Task<IReadOnlyList<RequestedStatement>> RequestStatementsAsync(string quarter, string actor, int? distributorId);
    Task<IReadOnlyList<StatementRow>> ListStatementsAsync(string? quarter);
    Task<StatementDocument?> GetStatementAsync(int statementId);
    Task<IReadOnlyList<AuditRow>> ListAuditAsync(string? entity, string? entityId);
    Task<string?> GetSettingAsync(string name);
}

public interface IDistributorApiStore
{
    Task<ApiClientInfo?> ResolveApiClientAsync(byte[] keyHash);
    Task<int> SubmitClaimAsync(int distributorId, int period, string? reference, string submittedBy, IReadOnlyList<ClaimLineInput> lines);
    Task<ClaimDetail?> GetClaimAsync(int claimId);
    Task<IReadOnlyList<DistributorClaim>> ListClaimsAsync(int distributorId, int? period);
    Task<IReadOnlyList<DistributorAgreement>> ListAgreementsAsync(int distributorId);
}

namespace Lucerne.Lending.CoreBanking;

/// <summary>Customer master (CIF) in the core banking system.</summary>
public interface ICoreCustomerService
{
    Task<CoreCustomer?> FindByPersonalIdentityCodeAsync(string personalIdentityCode, CancellationToken ct = default);
    Task<CoreCustomer?> GetAsync(string customerNumber, CancellationToken ct = default);
    Task<IReadOnlyList<CoreAccount>> GetAccountsAsync(string customerNumber, CancellationToken ct = default);
    Task<CoreExposure> GetExposureAsync(string customerNumber, CancellationToken ct = default);
    Task<string> CreateCustomerAsync(NewCustomerRequest request, CancellationToken ct = default);
}

/// <summary>Loan ledger in the core banking system.</summary>
public interface ICoreLoanService
{
    Task<LoanBookingResult> BookAsync(LoanBookingRequest request, CancellationToken ct = default);
    Task<DisbursementResult> DisburseAsync(DisbursementRequest request, CancellationToken ct = default);
    Task<CoreLoanSummary?> GetLoanAsync(string accountNumber, CancellationToken ct = default);
    Task<IReadOnlyList<CoreLoanSummary>> ListLoansAsync(string customerNumber, CancellationToken ct = default);
    Task<IReadOnlyList<CorePayment>> GetPaymentHistoryAsync(string accountNumber, DateOnly from, CancellationToken ct = default);
    Task<CoreOperationResult> ChargeFeeAsync(string accountNumber, string feeCode, decimal amount, string text, CancellationToken ct = default);
    Task<CoreOperationResult> SetPaymentPlanAsync(string accountNumber, IReadOnlyList<PlanInstalmentRequest> instalments, string officer, CancellationToken ct = default);
    Task<CoreOperationResult> TerminateAsync(string accountNumber, DateOnly effectiveDate, string officer, CancellationToken ct = default);
}

/// <summary>Loan events published by the core end-of-day and payment posting.</summary>
public interface ICoreEventSource
{
    /// <summary>Waits up to <paramref name="wait"/> for the next event; null when none arrived.</summary>
    Task<CoreLoanEvent?> ReceiveAsync(TimeSpan wait, CancellationToken ct = default);
}

public interface ICoreReferenceData
{
    /// <summary>Reference rate in percent for code E3M, E12 or FIX at the given date.</summary>
    Task<decimal> GetReferenceRateAsync(string code, DateOnly date, CancellationToken ct = default);
    Task<DateOnly> GetBusinessDateAsync(CancellationToken ct = default);
}

using System.Data;

namespace Lucerne.Lending.Data;

/// <summary>Lending's copy of the booked loans, updated from core events (the core ledger is the master).</summary>
public sealed class LoanRepository
{
    private readonly Db _db;

    public LoanRepository(Db db) => _db = db;

    public Task AddAsync(LoanRow loan, IDbTransaction? tx = null) =>
        _db.ExecuteAsync(@"INSERT INTO lending.Loan(AccountNumber, ApplicationId, CustomerNumber, ProductCode, Amount, NominalRatePercent,
                MonthlyInstalment, BookedUtc, DisbursedUtc, Balance, Arrears, DaysPastDue, CoreStatus, LastEventUtc)
            VALUES (@AccountNumber, @ApplicationId, @CustomerNumber, @ProductCode, @Amount, @NominalRatePercent, @MonthlyInstalment, @BookedUtc,
                @DisbursedUtc, @Balance, @Arrears, @DaysPastDue, @CoreStatus, @LastEventUtc)", loan, tx);

    public Task<LoanRow?> GetAsync(string accountNumber, CancellationToken ct = default) =>
        _db.FirstOrDefaultAsync<LoanRow>("SELECT * FROM lending.Loan WHERE AccountNumber = @accountNumber", new { accountNumber }, ct: ct);

    public Task MarkDisbursedAsync(string accountNumber, decimal balance, IDbTransaction? tx = null) =>
        _db.ExecuteAsync("UPDATE lending.Loan SET DisbursedUtc = @now, Balance = @balance, CoreStatus = 'ACTV' WHERE AccountNumber = @accountNumber",
            new { accountNumber, balance, now = DateTime.UtcNow }, tx);

    public Task UpdateFromCoreAsync(string accountNumber, decimal? balance, decimal arrears, int dpd, string? status, DateTime eventTime,
        IDbTransaction? tx = null) =>
        _db.ExecuteAsync(@"UPDATE lending.Loan SET Balance = ISNULL(@balance, Balance), Arrears = @arrears, DaysPastDue = @dpd,
                CoreStatus = ISNULL(@status, CoreStatus), LastEventUtc = @eventTime WHERE AccountNumber = @accountNumber",
            new { accountNumber, balance, arrears, dpd, status, eventTime }, tx);

    public Task<IReadOnlyList<LoanRow>> ListAsync(bool activeOnly, CancellationToken ct = default) =>
        _db.QueryAsync<LoanRow>(activeOnly
            ? "SELECT * FROM lending.Loan WHERE CoreStatus NOT IN ('CLSD', 'BOOK') ORDER BY AccountNumber"
            : "SELECT * FROM lending.Loan ORDER BY AccountNumber", ct: ct);

    public Task<IReadOnlyList<LoanRow>> ForCustomerAsync(string customerNumber, CancellationToken ct = default) =>
        _db.QueryAsync<LoanRow>("SELECT * FROM lending.Loan WHERE CustomerNumber = @customerNumber ORDER BY BookedUtc", new { customerNumber }, ct: ct);
}

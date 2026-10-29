using Dapper;
using Lucerne.Lending.Domain;
using Microsoft.Data.Sqlite;

namespace Lucerne.Lending.CoreBanking.Simulator;

internal sealed class LoanRow
{
    public string acct { get; set; } = "";
    public string cif { get; set; } = "";
    public string? cocif { get; set; }
    public string? appref { get; set; }
    public string product { get; set; } = "";
    public decimal original { get; set; }
    public decimal balance { get; set; }
    public decimal rate { get; set; }
    public decimal monthly_fee { get; set; }
    public decimal instalment { get; set; }
    public int due_day { get; set; }
    public string? next_due { get; set; }
    public decimal arrears { get; set; }
    public string? oldest_unpaid { get; set; }
    public int dpd { get; set; }
    public string status { get; set; } = "";
    public int term { get; set; }
    public int disbursed { get; set; }
    public int plan { get; set; }

    public CoreLoanSummary ToSummary() => new(acct, product, original, balance, rate,
        next_due == null ? null : SimulatorDatabase.P(next_due), instalment + monthly_fee, arrears, dpd, status, cif);
}

internal sealed class SimulatorCustomerService : ICoreCustomerService
{
    private readonly SimulatorDatabase _db;

    public SimulatorCustomerService(SimulatorDatabase db) => _db = db;

    public Task<CoreCustomer?> FindByPersonalIdentityCodeAsync(string personalIdentityCode, CancellationToken ct = default) =>
        Task.FromResult(Find("hetu", personalIdentityCode.ToUpperInvariant()));

    public Task<CoreCustomer?> GetAsync(string customerNumber, CancellationToken ct = default) =>
        Task.FromResult(Find("cif", customerNumber));

    private CoreCustomer? Find(string column, string value)
    {
        using var cn = _db.Open();
        var r = cn.QueryFirstOrDefault($"SELECT * FROM customer WHERE {column} = @value", new { value });
        if (r == null) return null;
        return new CoreCustomer((string)r.cif, (string)r.hetu, (string)r.name, (string)r.street, (string)r.postal, (string)r.city,
            (string)r.segment, (string)r.kyc, SimulatorDatabase.P((string)r.since), (string)r.rating, (long)r.deceased == 1,
            (long)r.blocked == 1, (string)r.branch);
    }

    public Task<IReadOnlyList<CoreAccount>> GetAccountsAsync(string customerNumber, CancellationToken ct = default)
    {
        using var cn = _db.Open();
        IReadOnlyList<CoreAccount> list = cn.Query("SELECT * FROM account WHERE cif = @customerNumber ORDER BY opened, acct", new { customerNumber })
            .Select(r => new CoreAccount((string)r.acct, (string)r.iban, (string)r.product, (string)r.type, Convert.ToDecimal(r.balance), (string)r.role))
            .ToList();
        return Task.FromResult(list);
    }

    public Task<CoreExposure> GetExposureAsync(string customerNumber, CancellationToken ct = default)
    {
        using var cn = _db.Open();
        var loans = cn.Query<LoanRow>("SELECT * FROM loan WHERE (cif = @customerNumber OR cocif = @customerNumber) AND status <> 'CLSD'",
            new { customerNumber }).Select(l => l.ToSummary()).ToList();
        var exposure = new CoreExposure(customerNumber, loans.Sum(l => l.OriginalAmount), loans.Sum(l => l.Balance),
            loans.Sum(l => l.Arrears), loans.Count == 0 ? 0 : loans.Max(l => l.DaysPastDue), loans.Count,
            loans.Sum(l => l.NextInstalment), loans);
        return Task.FromResult(exposure);
    }

    public Task<string> CreateCustomerAsync(NewCustomerRequest request, CancellationToken ct = default)
    {
        using var cn = _db.Open();
        using var tx = cn.BeginTransaction();
        var existing = cn.ExecuteScalar<string?>("SELECT cif FROM customer WHERE hetu = @h", new { h = request.PersonalIdentityCode.ToUpperInvariant() }, tx);
        if (existing != null) return Task.FromResult(existing);
        var cif = SimulatorDatabase.Next(cn, "cif", tx).ToString("0000000000");
        cn.Execute(@"INSERT INTO customer(cif, hetu, name, street, postal, city, segment, kyc, since, rating, branch)
                     VALUES (@cif, @hetu, @name, @street, @postal, @city, 'PRI', 'P', @since, '  ', @branch)",
            new
            {
                cif,
                hetu = request.PersonalIdentityCode.ToUpperInvariant(),
                name = $"{request.LastName} {request.FirstName}".ToUpperInvariant(),
                street = request.Street,
                postal = request.PostalCode,
                city = request.City.ToUpperInvariant(),
                since = SimulatorDatabase.D(SimulatorDatabase.BusinessDate(cn, tx)),
                branch = request.BranchCode
            }, tx);
        tx.Commit();
        return Task.FromResult(cif);
    }
}

internal sealed class SimulatorLoanService : ICoreLoanService
{
    private readonly SimulatorDatabase _db;

    public SimulatorLoanService(SimulatorDatabase db) => _db = db;

    public Task<LoanBookingResult> BookAsync(LoanBookingRequest r, CancellationToken ct = default)
    {
        using var cn = _db.Open();
        using var tx = cn.BeginTransaction();
        var existing = cn.ExecuteScalar<string?>("SELECT acct FROM loan WHERE appref = @a", new { a = r.ApplicationReference }, tx);
        if (existing != null) return Task.FromResult(new LoanBookingResult(true, existing, CoreReturnCodes.Duplicate, "Application already booked"));
        if (cn.ExecuteScalar<long>("SELECT COUNT(*) FROM customer WHERE cif = @c AND blocked = 0", new { c = r.CustomerNumber }, tx) == 0)
            return Task.FromResult(new LoanBookingResult(false, null, CoreReturnCodes.Rejected, "Customer not found or blocked"));
        var acct = "47908510" + SimulatorDatabase.Next(cn, "loan", tx).ToString("0000");
        cn.Execute(@"INSERT INTO loan(acct, cif, cocif, appref, product, original, balance, rate, margin, refrate, monthly_fee, instalment,
                       due_day, next_due, status, opened, term)
                     VALUES (@acct, @cif, @cocif, @appref, @product, @amount, 0, @rate, @margin, @refrate, @fee, @instalment,
                       @dueDay, @firstDue, 'BOOK', @opened, @term)",
            new
            {
                acct, cif = r.CustomerNumber, cocif = r.CoBorrowerNumber, appref = r.ApplicationReference, product = r.CoreProductCode,
                amount = r.Amount, rate = r.NominalRatePercent, margin = r.MarginPercent, refrate = r.ReferenceRateCode, fee = r.MonthlyFee,
                instalment = Annuity.MonthlyPayment(r.Amount, r.NominalRatePercent, r.TermMonths), dueDay = r.DueDay,
                firstDue = SimulatorDatabase.D(r.FirstDueDate), opened = SimulatorDatabase.D(SimulatorDatabase.BusinessDate(cn, tx)),
                term = r.TermMonths
            }, tx);
        tx.Commit();
        return Task.FromResult(new LoanBookingResult(true, acct, CoreReturnCodes.Ok, "Loan account opened"));
    }

    public Task<DisbursementResult> DisburseAsync(DisbursementRequest r, CancellationToken ct = default)
    {
        using var cn = _db.Open();
        using var tx = cn.BeginTransaction();
        var loan = cn.QueryFirstOrDefault<LoanRow>("SELECT * FROM loan WHERE acct = @a", new { a = r.AccountNumber }, tx);
        if (loan == null) return Task.FromResult(new DisbursementResult(false, null, false, CoreReturnCodes.NotFound, "Loan account not found"));
        if (loan.disbursed == 1) return Task.FromResult(new DisbursementResult(false, null, false, CoreReturnCodes.Duplicate, "Already disbursed"));
        if (r.Amount != loan.original) return Task.FromResult(new DisbursementResult(false, null, false, CoreReturnCodes.Rejected, "Amount differs from loan amount"));
        var date = SimulatorDatabase.D(SimulatorDatabase.BusinessDate(cn, tx));
        var iban = Iban.Normalize(r.TargetIban);
        var own = cn.ExecuteScalar<string?>("SELECT acct FROM account WHERE iban = @iban", new { iban }, tx);
        if (own != null) cn.Execute("UPDATE account SET balance = balance + @amt WHERE acct = @own", new { amt = r.Amount, own }, tx);
        cn.Execute("UPDATE loan SET balance = original, disbursed = 1, status = 'ACTV' WHERE acct = @a", new { a = r.AccountNumber }, tx);
        cn.Execute(@"INSERT INTO txn(acct, date, type, amount, principal, interest, fees, balance_after)
                     VALUES (@a, @date, 'DB', @amt, @amt, 0, 0, @amt)", new { a = r.AccountNumber, date, amt = r.Amount }, tx);
        Event(cn, tx, CoreLoanEvent.Disbursed, r.AccountNumber, r.Amount, 0, 0);
        var txn = "S" + SimulatorDatabase.Next(cn, "txn", tx).ToString("0000000000");
        tx.Commit();
        return Task.FromResult(new DisbursementResult(true, txn, own == null, CoreReturnCodes.Ok, own == null ? "SEPA transfer created" : "Credited to own account"));
    }

    public Task<CoreLoanSummary?> GetLoanAsync(string accountNumber, CancellationToken ct = default)
    {
        using var cn = _db.Open();
        return Task.FromResult(cn.QueryFirstOrDefault<LoanRow>("SELECT * FROM loan WHERE acct = @accountNumber", new { accountNumber })?.ToSummary());
    }

    public Task<IReadOnlyList<CoreLoanSummary>> ListLoansAsync(string customerNumber, CancellationToken ct = default)
    {
        using var cn = _db.Open();
        IReadOnlyList<CoreLoanSummary> list = cn.Query<LoanRow>("SELECT * FROM loan WHERE cif = @customerNumber OR cocif = @customerNumber ORDER BY opened",
            new { customerNumber }).Select(l => l.ToSummary()).ToList();
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<CorePayment>> GetPaymentHistoryAsync(string accountNumber, DateOnly from, CancellationToken ct = default)
    {
        using var cn = _db.Open();
        IReadOnlyList<CorePayment> list = cn.Query("SELECT * FROM txn WHERE acct = @accountNumber AND date >= @from ORDER BY date, id",
                new { accountNumber, from = SimulatorDatabase.D(from) })
            .Select(t => new CorePayment(SimulatorDatabase.P((string)t.date), TypeName((string)t.type), Convert.ToDecimal(t.amount),
                Convert.ToDecimal(t.principal), Convert.ToDecimal(t.interest), Convert.ToDecimal(t.fees), Convert.ToDecimal(t.balance_after)))
            .ToList();
        return Task.FromResult(list);
    }

    private static string TypeName(string code) => code switch
    {
        "DB" => "Disbursement",
        "PM" => "Payment",
        "FE" => "Fee",
        "RF" => "ReminderFee",
        "TM" => "Termination",
        _ => code
    };

    public Task<CoreOperationResult> ChargeFeeAsync(string accountNumber, string feeCode, decimal amount, string text, CancellationToken ct = default)
    {
        using var cn = _db.Open();
        using var tx = cn.BeginTransaction();
        var loan = cn.QueryFirstOrDefault<LoanRow>("SELECT * FROM loan WHERE acct = @a", new { a = accountNumber }, tx);
        if (loan == null) return Task.FromResult(new CoreOperationResult(false, CoreReturnCodes.NotFound, "Loan account not found"));
        cn.Execute("UPDATE loan SET arrears = arrears + @amount WHERE acct = @a", new { amount, a = accountNumber }, tx);
        cn.Execute(@"INSERT INTO txn(acct, date, type, amount, principal, interest, fees, balance_after)
                     VALUES (@a, @date, @type, @amount, 0, 0, @amount, @bal)",
            new { a = accountNumber, date = SimulatorDatabase.D(SimulatorDatabase.BusinessDate(cn, tx)), type = feeCode == "REM" ? "RF" : "FE", amount, bal = loan.balance }, tx);
        tx.Commit();
        return Task.FromResult(CoreOperationResult.Ok("Fee charged"));
    }

    public Task<CoreOperationResult> SetPaymentPlanAsync(string accountNumber, IReadOnlyList<PlanInstalmentRequest> instalments, string officer, CancellationToken ct = default)
    {
        using var cn = _db.Open();
        using var tx = cn.BeginTransaction();
        if (cn.ExecuteScalar<long>("SELECT COUNT(*) FROM loan WHERE acct = @a", new { a = accountNumber }, tx) == 0)
            return Task.FromResult(new CoreOperationResult(false, CoreReturnCodes.NotFound, "Loan account not found"));
        cn.Execute("DELETE FROM plan WHERE acct = @a", new { a = accountNumber }, tx);
        var n = 1;
        foreach (var i in instalments)
            cn.Execute("INSERT INTO plan(acct, n, due, amount) VALUES (@a, @n, @due, @amount)",
                new { a = accountNumber, n = n++, due = SimulatorDatabase.D(i.DueDate), amount = i.Amount }, tx);
        cn.Execute("UPDATE loan SET plan = 1, status = CASE WHEN status = 'COLL' THEN 'COLL' ELSE 'PLAN' END WHERE acct = @a", new { a = accountNumber }, tx);
        tx.Commit();
        return Task.FromResult(CoreOperationResult.Ok("Payment plan registered"));
    }

    public Task<CoreOperationResult> TerminateAsync(string accountNumber, DateOnly effectiveDate, string officer, CancellationToken ct = default)
    {
        using var cn = _db.Open();
        var rows = cn.Execute("UPDATE loan SET status = 'TERM', arrears = arrears + balance WHERE acct = @a AND status <> 'TERM'", new { a = accountNumber });
        return Task.FromResult(rows == 0
            ? new CoreOperationResult(false, CoreReturnCodes.Rejected, "Loan not found or already terminated")
            : CoreOperationResult.Ok("Loan terminated"));
    }

    internal static void Event(SqliteConnection cn, SqliteTransaction tx, string type, string acct, decimal amount, decimal arrears, int dpd) =>
        cn.Execute("INSERT INTO evt(type, acct, ts, amount, arrears, dpd) VALUES (@type, @acct, @ts, @amount, @arrears, @dpd)",
            new { type, acct, ts = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), amount, arrears, dpd }, tx);
}

internal sealed class SimulatorEventSource : ICoreEventSource
{
    private readonly SimulatorDatabase _db;

    public SimulatorEventSource(SimulatorDatabase db) => _db = db;

    public async Task<CoreLoanEvent?> ReceiveAsync(TimeSpan wait, CancellationToken ct = default)
    {
        var until = DateTime.UtcNow + wait;
        do
        {
            using (var cn = _db.Open())
            using (var tx = cn.BeginTransaction())
            {
                var e = cn.QueryFirstOrDefault("SELECT * FROM evt ORDER BY id LIMIT 1", transaction: tx);
                if (e != null)
                {
                    cn.Execute("DELETE FROM evt WHERE id = @id", new { id = (long)e.id }, tx);
                    tx.Commit();
                    return new CoreLoanEvent((string)e.type, (string)e.acct, DateTime.Parse((string)e.ts), Convert.ToDecimal(e.amount),
                        Convert.ToDecimal(e.arrears), (int)(long)e.dpd, $"SIM:{e.id}");
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        } while (DateTime.UtcNow < until);
        return null;
    }
}

internal sealed class SimulatorReferenceData : ICoreReferenceData
{
    private readonly SimulatorDatabase _db;

    public SimulatorReferenceData(SimulatorDatabase db) => _db = db;

    public Task<decimal> GetReferenceRateAsync(string code, DateOnly date, CancellationToken ct = default)
    {
        using var cn = _db.Open();
        var rate = cn.ExecuteScalar<decimal?>("SELECT rate FROM refrate WHERE code = @code AND valid <= @d ORDER BY valid DESC LIMIT 1",
            new { code, d = SimulatorDatabase.D(date) });
        return rate.HasValue ? Task.FromResult(rate.Value) : throw new CoreBankingException($"No reference rate {code} on {date}.");
    }

    public Task<DateOnly> GetBusinessDateAsync(CancellationToken ct = default)
    {
        using var cn = _db.Open();
        return Task.FromResult(SimulatorDatabase.BusinessDate(cn));
    }
}

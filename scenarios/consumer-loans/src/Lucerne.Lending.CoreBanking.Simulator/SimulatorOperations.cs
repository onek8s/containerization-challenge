using Dapper;
using Lucerne.Lending.Domain;

namespace Lucerne.Lending.CoreBanking.Simulator;

public sealed record EndOfDayResult(DateOnly BusinessDate, int DueLoans, int Missed, int DpdUpdates);

/// <summary>Training operations: end of day and incoming payments, as the core batch would do them.</summary>
public sealed class SimulatorOperations
{
    private readonly SimulatorDatabase _db;

    public SimulatorOperations(SimulatorDatabase db) => _db = db;

    /// <summary>Moves the business date forward one day and processes due dates and days past due.</summary>
    public EndOfDayResult RunEndOfDay()
    {
        using var cn = _db.Open();
        using var tx = cn.BeginTransaction();
        var date = SimulatorDatabase.BusinessDate(cn, tx).AddDays(1);
        cn.Execute("UPDATE busdate SET date = @d WHERE id = 1", new { d = SimulatorDatabase.D(date) }, tx);
        int due = 0, missed = 0, dpdUpdates = 0;
        var loans = cn.Query<LoanRow>("SELECT * FROM loan WHERE disbursed = 1 AND status IN ('ACTV', 'PLAN', 'COLL', 'TERM')", transaction: tx).ToList();
        foreach (var loan in loans)
        {
            if (loan.next_due != null && SimulatorDatabase.P(loan.next_due) == date.AddDays(7))
            {
                due++;
                SimulatorLoanService.Event(cn, tx, CoreLoanEvent.Due, loan.acct, loan.instalment + loan.monthly_fee, loan.arrears, loan.dpd);
            }
            if (loan.next_due != null && SimulatorDatabase.P(loan.next_due) <= date)
            {
                var amount = loan.instalment + loan.monthly_fee;
                var oldest = loan.oldest_unpaid ?? loan.next_due;
                var next = DueDates.Add(SimulatorDatabase.P(loan.next_due), 1);
                cn.Execute("UPDATE loan SET arrears = arrears + @amount, oldest_unpaid = @oldest, next_due = @next WHERE acct = @acct",
                    new { amount, oldest, next = SimulatorDatabase.D(next), loan.acct }, tx);
                loan.arrears += amount;
                loan.oldest_unpaid = oldest;
                missed++;
                SimulatorLoanService.Event(cn, tx, CoreLoanEvent.Missed, loan.acct, amount, loan.arrears, loan.dpd);
            }
            if (loan.oldest_unpaid != null && loan.arrears > 0)
            {
                var dpd = date.DayNumber - SimulatorDatabase.P(loan.oldest_unpaid).DayNumber;
                if (dpd != loan.dpd)
                {
                    cn.Execute("UPDATE loan SET dpd = @dpd, status = CASE WHEN @dpd >= 90 AND status = 'ACTV' THEN 'COLL' ELSE status END WHERE acct = @acct",
                        new { dpd, loan.acct }, tx);
                    dpdUpdates++;
                    SimulatorLoanService.Event(cn, tx, CoreLoanEvent.DaysPastDueChanged, loan.acct, 0, loan.arrears, dpd);
                }
            }
        }
        tx.Commit();
        return new EndOfDayResult(date, due, missed, dpdUpdates);
    }

    /// <summary>Posts an incoming reference payment: arrears first, then extra repayment of principal.</summary>
    public decimal PostPayment(string accountNumber, decimal amount)
    {
        using var cn = _db.Open();
        using var tx = cn.BeginTransaction();
        var loan = cn.QueryFirstOrDefault<LoanRow>("SELECT * FROM loan WHERE acct = @a", new { a = accountNumber }, tx)
                   ?? throw new CoreBankingException("Loan account not found") { ReturnCode = CoreReturnCodes.NotFound };
        var date = SimulatorDatabase.BusinessDate(cn, tx);
        var toArrears = Math.Min(amount, loan.arrears);
        var extra = amount - toArrears;
        var interest = Money.Round(Math.Min(toArrears, loan.balance * loan.rate / 1200m));
        var principal = Math.Min(loan.balance, toArrears - interest + extra);
        var balance = loan.balance - principal;
        var arrears = loan.arrears - toArrears;
        var status = balance <= 0 ? "CLSD" : loan.status == "COLL" && arrears == 0 ? "ACTV" : loan.status;
        cn.Execute(@"UPDATE loan SET balance = @balance, arrears = @arrears, dpd = CASE WHEN @arrears = 0 THEN 0 ELSE dpd END,
                       oldest_unpaid = CASE WHEN @arrears = 0 THEN NULL ELSE oldest_unpaid END, status = @status WHERE acct = @a",
            new { balance, arrears, status, a = accountNumber }, tx);
        cn.Execute(@"INSERT INTO txn(acct, date, type, amount, principal, interest, fees, balance_after)
                     VALUES (@a, @date, 'PM', @amount, @principal, @interest, 0, @balance)",
            new { a = accountNumber, date = SimulatorDatabase.D(date), amount, principal, interest, balance }, tx);
        SimulatorLoanService.Event(cn, tx, balance <= 0 ? CoreLoanEvent.Settled : CoreLoanEvent.Paid, accountNumber, amount, arrears,
            arrears == 0 ? 0 : loan.dpd);
        tx.Commit();
        return arrears;
    }

    public IReadOnlyList<(string Account, string Customer, decimal Balance, decimal Arrears, int Dpd, string Status, string? NextDue)> Loans()
    {
        using var cn = _db.Open();
        return cn.Query<LoanRow>("SELECT * FROM loan ORDER BY acct").Select(l => (l.acct, l.cif, l.balance, l.arrears, l.dpd, l.status, l.next_due)).ToList();
    }

    public DateOnly BusinessDate()
    {
        using var cn = _db.Open();
        return SimulatorDatabase.BusinessDate(cn);
    }
}

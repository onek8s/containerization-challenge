using System.Globalization;
using System.Text;
using IBM.Data.Db2;
using Lucerne.Lending.Data;
using Lucerne.Lending.LoanTape;
using Microsoft.Extensions.Configuration;
using NLog;

// Monthly loan tape to the group risk data warehouse (DB2 LUW, database RISKDW).
// Run by Task Scheduler on the first working day: Lucerne.Lending.LoanTape.exe [--date 2023-05-31] [--target Db2|Folder] [--first-working-day-only true]
var log = LogManager.GetLogger("LoanTape");
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
    .AddEnvironmentVariables("LOANTAPE_")
    .AddCommandLine(args)
    .Build();

var snapshot = configuration["date"] is { } d
    ? DateOnly.ParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture)
    : DateOnly.FromDateTime(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddDays(-1));
var target = configuration["target"] ?? configuration["LoanTape:Target"] ?? "Db2";

// the scheduled task fires every day; the tape is produced on the first working day (Mon-Fri) of the month
if (string.Equals(configuration["first-working-day-only"], "true", StringComparison.OrdinalIgnoreCase) && configuration["date"] == null)
{
    var first = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    while (first.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) first = first.AddDays(1);
    if (DateTime.Today != first)
    {
        log.Info("Not the first working day of the month, nothing to do");
        LogManager.Shutdown();
        return 0;
    }
}

try
{
    var db = new Db(LendingDbOptions.From(configuration));
    var rows = await db.QueryAsync<LoanTapeRow>(@"
        SELECT l.AccountNumber, l.CustomerNumber, l.ProductCode, a.Reference AS ApplicationReference, a.Channel, l.BookedUtc, l.DisbursedUtc,
               l.Amount, l.Balance, l.Arrears, l.DaysPastDue, l.NominalRatePercent, a.TermMonths, l.CoreStatus,
               (SELECT TOP (1) d.RiskGrade FROM lending.Decision d WHERE d.ApplicationId = a.Id ORDER BY d.Id DESC) AS RiskGrade,
               (SELECT TOP (1) d.Score FROM lending.Decision d WHERE d.ApplicationId = a.Id ORDER BY d.Id DESC) AS Score,
               CASE WHEN EXISTS (SELECT 1 FROM lending.Applicant p WHERE p.ApplicationId = a.Id AND p.IsMain = 0) THEN 1 ELSE 0 END AS CoApplicant
        FROM lending.Loan l LEFT JOIN lending.Application a ON a.Id = l.ApplicationId
        WHERE l.CoreStatus NOT IN ('BOOK') AND l.BookedUtc < @until
        ORDER BY l.AccountNumber".Replace("SELECT TOP (1) d.RiskGrade", db.IsSqlite ? "SELECT d.RiskGrade" : "SELECT TOP (1) d.RiskGrade")
        .Replace("SELECT TOP (1) d.Score", db.IsSqlite ? "SELECT d.Score" : "SELECT TOP (1) d.Score")
        .Replace("ORDER BY d.Id DESC)", db.IsSqlite ? "ORDER BY d.Id DESC LIMIT 1)" : "ORDER BY d.Id DESC)"),
        new { until = snapshot.AddDays(1).ToDateTime(TimeOnly.MinValue) });
    log.Info("Loan tape {0}: {1} loans, target {2}", snapshot, rows.Count, target);

    if (string.Equals(target, "Folder", StringComparison.OrdinalIgnoreCase))
        WriteFolder(configuration["LoanTape:Folder"] ?? @"D:\Interfaces\RiskDW\Outbound", snapshot, rows);
    else
        await WriteDb2Async(configuration.GetConnectionString("RiskDw")!, configuration["LoanTape:Schema"] ?? "LOANTAPE", snapshot, rows);
    return 0;
}
catch (Exception ex)
{
    log.Error(ex, "Loan tape {0} failed", snapshot);
    return 1;
}
finally
{
    LogManager.Shutdown();
}

static async Task WriteDb2Async(string connectionString, string schema, DateOnly snapshot, IReadOnlyList<LoanTapeRow> rows)
{
    using var cn = new DB2Connection(connectionString);
    await cn.OpenAsync();
    using var tx = cn.BeginTransaction();
    using (var del = new DB2Command($"DELETE FROM {schema}.CL_LOAN_TAPE_STG WHERE SNAPSHOT_DATE = @date", cn, tx))
    {
        del.Parameters.Add("@date", DB2Type.Date).Value = snapshot.ToDateTime(TimeOnly.MinValue);
        await del.ExecuteNonQueryAsync();
    }
    using (var ins = new DB2Command($@"INSERT INTO {schema}.CL_LOAN_TAPE_STG
            (SNAPSHOT_DATE, SOURCE_SYSTEM, ACCOUNT_NO, CUSTOMER_NO, PRODUCT_CODE, APPLICATION_REF, CHANNEL, BOOKED_TS, DISBURSED_TS, ORIG_AMOUNT,
             BALANCE, ARREARS, DPD, NOMINAL_RATE, TERM_MONTHS, RISK_GRADE, SCORE, IFRS9_STAGE, CORE_STATUS, CO_BORROWER)
        VALUES (@date, 'LUCLEND', @acct, @cust, @prod, @app, @chan, @booked, @disb, @amt, @bal, @arr, @dpd, @rate, @term, @grade, @score, @stage, @status, @co)",
        cn, tx))
    {
        ins.Parameters.Add("@date", DB2Type.Date);
        ins.Parameters.Add("@acct", DB2Type.Char, 12);
        ins.Parameters.Add("@cust", DB2Type.Char, 10);
        ins.Parameters.Add("@prod", DB2Type.VarChar, 10);
        ins.Parameters.Add("@app", DB2Type.VarChar, 20);
        ins.Parameters.Add("@chan", DB2Type.SmallInt);
        ins.Parameters.Add("@booked", DB2Type.Timestamp);
        ins.Parameters.Add("@disb", DB2Type.Timestamp);
        ins.Parameters.Add("@amt", DB2Type.Decimal);
        ins.Parameters.Add("@bal", DB2Type.Decimal);
        ins.Parameters.Add("@arr", DB2Type.Decimal);
        ins.Parameters.Add("@dpd", DB2Type.Integer);
        ins.Parameters.Add("@rate", DB2Type.Decimal);
        ins.Parameters.Add("@term", DB2Type.SmallInt);
        ins.Parameters.Add("@grade", DB2Type.Char, 1);
        ins.Parameters.Add("@score", DB2Type.SmallInt);
        ins.Parameters.Add("@stage", DB2Type.SmallInt);
        ins.Parameters.Add("@status", DB2Type.Char, 4);
        ins.Parameters.Add("@co", DB2Type.Char, 1);
        ins.Prepare();
        foreach (var r in rows)
        {
            ins.Parameters["@date"].Value = snapshot.ToDateTime(TimeOnly.MinValue);
            ins.Parameters["@acct"].Value = r.AccountNumber;
            ins.Parameters["@cust"].Value = r.CustomerNumber;
            ins.Parameters["@prod"].Value = r.ProductCode;
            ins.Parameters["@app"].Value = (object?)r.ApplicationReference ?? DBNull.Value;
            ins.Parameters["@chan"].Value = (object?)r.Channel ?? DBNull.Value;
            ins.Parameters["@booked"].Value = r.BookedUtc;
            ins.Parameters["@disb"].Value = (object?)r.DisbursedUtc ?? DBNull.Value;
            ins.Parameters["@amt"].Value = r.Amount;
            ins.Parameters["@bal"].Value = r.Balance;
            ins.Parameters["@arr"].Value = r.Arrears;
            ins.Parameters["@dpd"].Value = r.DaysPastDue;
            ins.Parameters["@rate"].Value = r.NominalRatePercent;
            ins.Parameters["@term"].Value = (object?)r.TermMonths ?? DBNull.Value;
            ins.Parameters["@grade"].Value = (object?)r.RiskGrade ?? DBNull.Value;
            ins.Parameters["@score"].Value = (object?)r.Score ?? DBNull.Value;
            ins.Parameters["@stage"].Value = r.Ifrs9Stage;
            ins.Parameters["@status"].Value = r.CoreStatus;
            ins.Parameters["@co"].Value = r.CoApplicant ? "Y" : "N";
            await ins.ExecuteNonQueryAsync();
        }
    }
    using (var publish = new DB2Command($"CALL {schema}.SP_PUBLISH_CL_SNAPSHOT(@date, @source)", cn, tx))
    {
        publish.Parameters.Add("@date", DB2Type.Date).Value = snapshot.ToDateTime(TimeOnly.MinValue);
        publish.Parameters.Add("@source", DB2Type.VarChar, 10).Value = "LUCLEND";
        await publish.ExecuteNonQueryAsync();
    }
    tx.Commit();
}

static void WriteFolder(string folder, DateOnly snapshot, IReadOnlyList<LoanTapeRow> rows)
{
    Directory.CreateDirectory(folder);
    var sb = new StringBuilder("SNAPSHOT_DATE;ACCOUNT_NO;CUSTOMER_NO;PRODUCT_CODE;APPLICATION_REF;ORIG_AMOUNT;BALANCE;ARREARS;DPD;NOMINAL_RATE;TERM_MONTHS;RISK_GRADE;SCORE;IFRS9_STAGE;CORE_STATUS\n");
    foreach (var r in rows)
        sb.AppendLine(string.Join(';', snapshot.ToString("yyyy-MM-dd"), r.AccountNumber, r.CustomerNumber, r.ProductCode, r.ApplicationReference,
            r.Amount.ToString("0.00", CultureInfo.InvariantCulture), r.Balance.ToString("0.00", CultureInfo.InvariantCulture), r.Arrears.ToString("0.00", CultureInfo.InvariantCulture),
            r.DaysPastDue, r.NominalRatePercent.ToString("0.0000", CultureInfo.InvariantCulture), r.TermMonths, r.RiskGrade, r.Score, r.Ifrs9Stage, r.CoreStatus));
    File.WriteAllText(Path.Combine(folder, $"CL_LOAN_TAPE_{snapshot:yyyyMMdd}.csv"), sb.ToString());
}

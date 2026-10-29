using Dapper;

namespace CityPermits.Data.Reporting;

public sealed record WorkloadRow(string? Officer, string Kind, int Open, int Overdue, int DueThisWeek);
public sealed record PerformanceRow(string Kind, string Period, int Decided, int WithinTarget, double PercentWithinTarget, double AverageDays);
public sealed record IncomeRow(string Code, string Description, int Lines, decimal Amount);
public sealed record OutstandingRow(string Reference, string Applicant, string Status, decimal Fees, decimal Paid, DateTime? SubmittedUtc);

/// <summary>
/// Read-only management reports (Dapper). SQL is written to run on SQL Server and on SQLite
/// so the development database can be used for report design.
/// </summary>
public sealed class ReportQueries
{
    private readonly ConnectionFactory _factory;

    public ReportQueries(ConnectionFactory factory) => _factory = factory;

    private string T(string table) => _factory.IsSqlite ? table : "permits." + table;

    public async Task<IReadOnlyList<WorkloadRow>> WorkloadAsync(DateTime today)
    {
        using var c = _factory.Open();
        var sql = $@"
SELECT AssignedOfficer AS Officer, Kind,
       COUNT(*) AS [Open],
       SUM(CASE WHEN TargetDecisionDate < @today THEN 1 ELSE 0 END) AS Overdue,
       SUM(CASE WHEN TargetDecisionDate >= @today AND TargetDecisionDate < @week THEN 1 ELSE 0 END) AS DueThisWeek
FROM {T("Cases")}
WHERE Status IN (20, 30, 40, 50, 60, 70)
GROUP BY AssignedOfficer, Kind
ORDER BY AssignedOfficer, Kind";
        var rows = await c.QueryAsync<(string? Officer, int Kind, int Open, int Overdue, int DueThisWeek)>(sql, new { today, week = today.AddDays(7) });
        return rows.Select(r => new WorkloadRow(r.Officer, ((Domain.Model.PermitKind)r.Kind).ToString(), r.Open, r.Overdue, r.DueThisWeek)).ToList();
    }

    public async Task<IReadOnlyList<PerformanceRow>> PerformanceAsync(DateTime from, DateTime to)
    {
        using var c = _factory.Open();
        var period = _factory.IsSqlite ? "strftime('%Y-%m', DecidedUtc)" : "FORMAT(DecidedUtc, 'yyyy-MM')";
        var days = _factory.IsSqlite ? "julianday(DecidedUtc) - julianday(SubmittedUtc)" : "DATEDIFF(hour, SubmittedUtc, DecidedUtc) / 24.0";
        var sql = $@"
SELECT Kind, {period} AS Period, COUNT(*) AS Decided,
       SUM(CASE WHEN DecidedUtc <= TargetDecisionDate THEN 1 ELSE 0 END) AS WithinTarget,
       AVG({days}) AS AverageDays
FROM {T("Cases")}
WHERE DecidedUtc >= @from AND DecidedUtc < @to AND SubmittedUtc IS NOT NULL
GROUP BY Kind, {period}
ORDER BY Period, Kind";
        var rows = await c.QueryAsync(sql, new { from, to });
        return rows.Select(r =>
        {
            int decided = Convert.ToInt32(r.Decided), within = Convert.ToInt32(r.WithinTarget);
            return new PerformanceRow(((Domain.Model.PermitKind)Convert.ToInt32(r.Kind)).ToString(), (string)r.Period, decided, within,
                decided == 0 ? 0 : Math.Round(100.0 * within / decided, 1), Math.Round(Convert.ToDouble(r.AverageDays ?? 0d), 1));
        }).ToList();
    }

    public async Task<IReadOnlyList<IncomeRow>> IncomeAsync(DateTime from, DateTime to)
    {
        using var c = _factory.Open();
        var sql = $@"
SELECT f.Code, MIN(f.Description) AS Description, COUNT(*) AS Lines, SUM(f.Amount) AS Amount
FROM {T("FeeLines")} f
JOIN {T("Cases")} c ON c.Id = f.CaseId
WHERE f.Waived = 0 AND f.CreatedUtc >= @from AND f.CreatedUtc < @to
  AND EXISTS (SELECT 1 FROM {T("Payments")} p WHERE p.CaseId = c.Id AND p.Status = 2)
GROUP BY f.Code
ORDER BY f.Code";
        var rows = await c.QueryAsync(sql, new { from, to });
        return rows.Select(r => new IncomeRow((string)r.Code, (string)r.Description, Convert.ToInt32(r.Lines),
            Convert.ToDecimal(r.Amount, System.Globalization.CultureInfo.InvariantCulture))).ToList();
    }

    public async Task<IReadOnlyList<OutstandingRow>> OutstandingAsync()
    {
        using var c = _factory.Open();
        var sql = $@"
SELECT c.Reference, a.DisplayName AS Applicant, c.Status,
       COALESCE((SELECT SUM(f.Amount) FROM {T("FeeLines")} f WHERE f.CaseId = c.Id AND f.Waived = 0), 0) AS Fees,
       COALESCE((SELECT SUM(p.Amount) FROM {T("Payments")} p WHERE p.CaseId = c.Id AND p.Status IN (1, 2)), 0) AS Paid,
       c.SubmittedUtc
FROM {T("Cases")} c JOIN {T("Applicants")} a ON a.Id = c.ApplicantId
WHERE c.Status NOT IN (0, 110)
ORDER BY c.SubmittedUtc";
        var rows = await c.QueryAsync(sql);
        return rows.Select(r => new OutstandingRow((string)r.Reference, (string)r.Applicant, ((Domain.Model.CaseStatus)Convert.ToInt32(r.Status)).ToString(),
                Convert.ToDecimal(r.Fees, System.Globalization.CultureInfo.InvariantCulture), Convert.ToDecimal(r.Paid, System.Globalization.CultureInfo.InvariantCulture),
                r.SubmittedUtc is null ? (DateTime?)null : Convert.ToDateTime(r.SubmittedUtc, System.Globalization.CultureInfo.InvariantCulture)))
            .Where(r => r.Fees > r.Paid).ToList();
    }
}

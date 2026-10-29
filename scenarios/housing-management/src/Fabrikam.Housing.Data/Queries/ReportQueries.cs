using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

namespace Fabrikam.Housing.Data.Queries
{
    public class ArrearsRow
    {
        public string TenancyReference { get; set; }
        public string PropertyReference { get; set; }
        public string Address { get; set; }
        public string Postcode { get; set; }
        public string PatchCode { get; set; }
        public string LeadTenant { get; set; }
        public decimal WeeklyCharge { get; set; }
        public decimal Balance { get; set; }
        public decimal WeeksInArrears => WeeklyCharge == 0 ? 0 : Math.Round(Balance / WeeklyCharge, 1);
        public int Stage { get; set; }
        public DateTime? LastPayment { get; set; }
        public bool HousingBenefit { get; set; }
        public bool UniversalCredit { get; set; }
    }

    public class RepairsPerformanceRow
    {
        public int Priority { get; set; }
        public string ContractorCode { get; set; }
        public int Completed { get; set; }
        public int CompletedInTarget { get; set; }
        public int OpenOverdue { get; set; }
        public double AverageDays { get; set; }
        public double PercentInTarget => Completed == 0 ? 0 : Math.Round(100.0 * CompletedInTarget / Completed, 1);
    }

    public class ComplianceRow
    {
        public string PropertyReference { get; set; }
        public string Address { get; set; }
        public string PatchCode { get; set; }
        public int Type { get; set; }
        public DateTime? InspectedOn { get; set; }
        public DateTime? ExpiresOn { get; set; }
        public string CertificateNumber { get; set; }
    }

    public class RentRollRow
    {
        public string PropertyReference { get; set; }
        public string Address { get; set; }
        public string EstateCode { get; set; }
        public int Bedrooms { get; set; }
        public int Status { get; set; }
        public string TenancyReference { get; set; }
        public decimal NetRent { get; set; }
        public decimal ServiceCharge { get; set; }
        public decimal WaterCharge { get; set; }
    }

    /// <summary>
    /// Reporting queries. Plain SQL through Dapper; kept to the subset that runs on SQL Server and SQLite.
    /// </summary>
    public class ReportQueries
    {
        private readonly ConnectionFactory _c;

        public ReportQueries(ConnectionFactory connections)
        {
            _c = connections;
        }

        public async Task<IList<ArrearsRow>> ArrearsAsync(decimal minimumBalance, string patch = null)
        {
            var sql = $@"
SELECT t.Reference AS TenancyReference, p.Reference AS PropertyReference, p.AddressLine1 AS Address, p.Postcode, p.PatchCode,
       (SELECT MIN(tn.FirstName || ' ' || tn.LastName) FROM {_c.Table("Tenants")} tn WHERE tn.TenancyId = t.Id AND tn.IsLeadTenant = 1) AS LeadTenant,
       t.WeeklyCharge, b.Balance, COALESCE(ac.Stage, 0) AS Stage, b.LastPayment,
       t.HousingBenefitClaimed AS HousingBenefit, t.UniversalCredit
FROM {_c.Table("Tenancies")} t
JOIN {_c.Table("Properties")} p ON p.Id = t.PropertyId
JOIN (SELECT TenancyId, SUM(Amount) AS Balance,
             MAX(CASE WHEN Amount < 0 THEN PostedOn END) AS LastPayment
      FROM {_c.Table("RentTransactions")} GROUP BY TenancyId) b ON b.TenancyId = t.Id
LEFT JOIN {_c.Table("ArrearsCases")} ac ON ac.TenancyId = t.Id AND ac.ClosedOn IS NULL
WHERE t.Status <> 3 AND b.Balance >= @minimumBalance AND (@patch IS NULL OR p.PatchCode = @patch)
ORDER BY b.Balance DESC";
            if (!_c.IsSqlite) sql = sql.Replace("tn.FirstName || ' ' || tn.LastName", "tn.FirstName + ' ' + tn.LastName");
            using (var c = _c.Open())
            {
                return (await c.QueryAsync<ArrearsRow>(sql, new { minimumBalance = (double)minimumBalance, patch })).ToList();
            }
        }

        public async Task<IList<RepairsPerformanceRow>> RepairsPerformanceAsync(DateTime from, DateTime to, DateTime now)
        {
            using (var c = _c.Open())
            {
                var rows = await c.QueryAsync<(int Priority, string ContractorCode, int Status, DateTime ReportedAt, DateTime TargetDate, DateTime? CompletedAt)>($@"
SELECT w.Priority, COALESCE(k.Code, '(none)'), w.Status, w.ReportedAt, w.TargetDate, w.CompletedAt
FROM {_c.Table("WorkOrders")} w LEFT JOIN {_c.Table("Contractors")} k ON k.Id = w.ContractorId
WHERE (w.CompletedAt >= @from AND w.CompletedAt < @to) OR (w.Status NOT IN (9, 10))", new { from, to = to.Date.AddDays(1) });

                return rows.GroupBy(r => new { r.Priority, r.ContractorCode })
                    .Select(g => new RepairsPerformanceRow
                    {
                        Priority = g.Key.Priority,
                        ContractorCode = g.Key.ContractorCode,
                        Completed = g.Count(r => r.Status == 9 && r.CompletedAt >= from),
                        CompletedInTarget = g.Count(r => r.Status == 9 && r.CompletedAt >= from && r.CompletedAt <= r.TargetDate),
                        OpenOverdue = g.Count(r => r.Status != 9 && r.Status != 10 && r.TargetDate < now),
                        AverageDays = Math.Round(g.Where(r => r.Status == 9 && r.CompletedAt >= from)
                            .Select(r => (r.CompletedAt.Value - r.ReportedAt).TotalDays).DefaultIfEmpty(0).Average(), 1)
                    })
                    .OrderBy(r => r.Priority).ThenBy(r => r.ContractorCode).ToList();
            }
        }

        public async Task<IList<ComplianceRow>> ComplianceAsync()
        {
            using (var c = _c.Open())
            {
                return (await c.QueryAsync<ComplianceRow>($@"
SELECT p.Reference AS PropertyReference, p.AddressLine1 AS Address, p.PatchCode, cc.Type, cc.InspectedOn, cc.ExpiresOn, cc.CertificateNumber
FROM {_c.Table("Properties")} p
JOIN {_c.Table("Certificates")} cc ON cc.PropertyId = p.Id AND cc.Superseded = 0
WHERE p.Status <> 3
ORDER BY cc.ExpiresOn")).ToList();
            }
        }

        public async Task<IList<RentRollRow>> RentRollAsync()
        {
            using (var c = _c.Open())
            {
                return (await c.QueryAsync<RentRollRow>($@"
SELECT p.Reference AS PropertyReference, p.AddressLine1 AS Address, e.Code AS EstateCode, p.Bedrooms, p.Status,
       t.Reference AS TenancyReference, p.WeeklyNetRent AS NetRent, p.WeeklyServiceCharge AS ServiceCharge, p.WeeklyWaterCharge AS WaterCharge
FROM {_c.Table("Properties")} p
JOIN {_c.Table("Estates")} e ON e.Id = p.EstateId
LEFT JOIN {_c.Table("Tenancies")} t ON t.PropertyId = p.Id AND t.Status <> 3
WHERE p.Status <> 3
ORDER BY e.Code, p.Reference")).ToList();
            }
        }

        public async Task<IDictionary<string, int>> DashboardCountsAsync(DateTime now)
        {
            using (var c = _c.Open())
            {
                var w = _c.Table("WorkOrders");
                var result = new Dictionary<string, int>
                {
                    ["OpenRepairs"] = await c.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {w} WHERE Status NOT IN (9, 10)"),
                    ["Emergencies"] = await c.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {w} WHERE Status NOT IN (9, 10) AND Priority = 1"),
                    ["OverdueRepairs"] = await c.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {w} WHERE Status NOT IN (9, 10) AND TargetDate < @now", new { now }),
                    ["AwaitingTriage"] = await c.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {w} WHERE Status = 1"),
                    ["OverdueCertificates"] = await c.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {_c.Table("Certificates")} WHERE Superseded = 0 AND ExpiresOn < @now", new { now }),
                    ["OpenEnquiries"] = await c.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {_c.Table("Enquiries")} WHERE RespondedAt IS NULL"),
                    ["InvoicesToApprove"] = await c.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {_c.Table("ContractorInvoices")} WHERE Status = 1")
                };
                return result;
            }
        }
    }
}

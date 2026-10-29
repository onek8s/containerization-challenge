using System.Data;
using Dapper;
using Humongous.Quoting.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Humongous.Quoting.Data;

/// <summary>
/// Read-side queries for lists, the dashboard, the referral queue and the bordereau.
/// Plain SQL that runs on SQL Server and on the SQLite development database.
/// </summary>
public sealed class QuotingQueries(QuotingDbContext db)
{
    static QuotingQueries()
    {
        // SQLite (development) returns GUID columns as text.
        SqlMapper.RemoveTypeMap(typeof(Guid));
        SqlMapper.RemoveTypeMap(typeof(Guid?));
        SqlMapper.AddTypeHandler(new GuidHandler());
    }

    private IDbConnection Connection => db.Database.GetDbConnection();

    public async Task<Dashboard> DashboardAsync(string? agencyCode, DateTime nowUtc)
    {
        var monthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var today = DateOnly.FromDateTime(nowUtc);
        var p = new { agencyCode, monthStart, today = today.ToDateTime(TimeOnly.MinValue), until = today.AddDays(60).ToDateTime(TimeOnly.MinValue) };
        var agencyFilter = agencyCode is null ? "" : " AND q.AgencyCode = @agencyCode";
        var policyFilter = agencyCode is null ? "" : " AND p.AgencyCode = @agencyCode";

        var byStatus = await Connection.QueryAsync<(string Status, long Count)>(
            $"SELECT q.Status AS Status, COUNT(*) AS Count FROM Quotes q WHERE 1 = 1{agencyFilter} GROUP BY q.Status ORDER BY q.Status", p);

        var bound = await Connection.ExecuteScalarAsync<decimal?>(
            $@"SELECT SUM(t.GrossPremium) FROM PolicyTransactions t
               JOIN Policies p ON p.Id = t.PolicyId
               WHERE t.CreatedUtc >= @monthStart{policyFilter}", p) ?? 0m;

        var live = await Connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM Policies p WHERE p.Status = 'Live'{policyFilter}", p);

        var renewals = await Connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM Policies p WHERE p.Status = 'Live' AND p.ExpiryDate >= @today AND p.ExpiryDate <= @until{policyFilter}", p);

        var recent = await QuoteListAsync(agencyCode, null, 0, 10);

        return new Dashboard
        {
            QuotesByStatus = byStatus.Select(s => new StatusCount(s.Status, (int)s.Count)).ToList(),
            BoundGrossPremiumMonthToDate = bound,
            PoliciesLive = live,
            RenewalsDueIn60Days = renewals,
            RecentQuotes = recent
        };
    }

    public async Task<IReadOnlyList<QuoteSummary>> QuoteListAsync(string? agencyCode, string? status, int skip, int take, string? search = null)
    {
        var sql = @"SELECT q.Id, q.Reference, q.AgencyCode, q.InsuredName, q.Postcode, q.TradeCode, q.Status,
                           q.Total, q.InceptionDate, q.CreatedUtc, q.ValidUntil, q.RenewalOfPolicyId
                    FROM Quotes q WHERE 1 = 1";
        if (agencyCode is not null) sql += " AND q.AgencyCode = @agencyCode";
        if (status is not null) sql += " AND q.Status = @status";
        if (!string.IsNullOrWhiteSpace(search)) sql += " AND (q.Reference LIKE @search OR q.InsuredName LIKE @search OR q.Postcode LIKE @search)";
        sql += " ORDER BY q.CreatedUtc DESC";
        sql += db.Database.IsSqlServer()
            ? " OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY"
            : " LIMIT @take OFFSET @skip";

        var rows = await Connection.QueryAsync<QuoteRow>(sql, new { agencyCode, status, skip, take, search = $"%{search}%" });
        return rows.Select(r => new QuoteSummary
        {
            Id = r.Id,
            Reference = r.Reference,
            AgencyCode = r.AgencyCode,
            InsuredName = r.InsuredName,
            Postcode = r.Postcode,
            TradeCode = r.TradeCode,
            Status = Enum.Parse<Domain.QuoteStatus>(r.Status),
            Total = r.Total,
            InceptionDate = DateOnly.FromDateTime(r.InceptionDate),
            CreatedUtc = DateTime.SpecifyKind(r.CreatedUtc, DateTimeKind.Utc),
            ValidUntil = r.ValidUntil is null ? null : DateOnly.FromDateTime(r.ValidUntil.Value),
            IsRenewal = r.RenewalOfPolicyId is not null
        }).ToList();
    }

    public async Task<IReadOnlyList<ReferralQueueItem>> ReferralQueueAsync()
    {
        var rows = await Connection.QueryAsync<ReferralRow>(
            @"SELECT q.Id AS QuoteId, q.Reference, q.AgencyCode, q.InsuredName, q.TradeCode, q.Total,
                     (SELECT COUNT(*) FROM QuoteReferrals r WHERE r.QuoteId = q.Id) AS ReferralCount,
                     q.ReferredUtc, q.InceptionDate, q.AssignedUnderwriter AS AssignedTo
              FROM Quotes q
              WHERE q.Status = 'Referred'
              ORDER BY q.InceptionDate, q.ReferredUtc");
        return rows.Select(r => new ReferralQueueItem
        {
            QuoteId = r.QuoteId,
            Reference = r.Reference,
            AgencyCode = r.AgencyCode,
            InsuredName = r.InsuredName,
            TradeCode = r.TradeCode,
            Total = r.Total,
            ReferralCount = r.ReferralCount,
            ReferredUtc = DateTime.SpecifyKind(r.ReferredUtc ?? DateTime.MinValue, DateTimeKind.Utc),
            InceptionDate = DateOnly.FromDateTime(r.InceptionDate),
            AssignedTo = r.AssignedTo
        }).ToList();
    }

    public async Task<IReadOnlyList<BordereauLine>> BordereauLinesAsync(DateTime fromUtc, DateTime toUtc)
    {
        var rows = await Connection.QueryAsync<BordereauLine>(
            @"SELECT t.Id AS TransactionId, p.PolicyNumber, p.AgencyCode, a.Name AS AgencyName, p.InsuredName, p.Postcode, p.TradeCode,
                     p.InceptionDate, p.ExpiryDate, t.Type AS TransactionType, t.EffectiveDate,
                     t.GrossPremium, t.Ipt, t.Commission, t.Fee, t.SectionGrossJson, t.CreatedUtc, p.ProductVersion
              FROM PolicyTransactions t
              JOIN Policies p ON p.Id = t.PolicyId
              JOIN Agencies a ON a.Code = p.AgencyCode
              WHERE t.CreatedUtc >= @fromUtc AND t.CreatedUtc < @toUtc
              ORDER BY t.CreatedUtc", new { fromUtc, toUtc });
        return rows.ToList();
    }

    private sealed class QuoteRow
    {
        public Guid Id { get; set; }
        public string Reference { get; set; } = "";
        public string AgencyCode { get; set; } = "";
        public string InsuredName { get; set; } = "";
        public string Postcode { get; set; } = "";
        public string TradeCode { get; set; } = "";
        public string Status { get; set; } = "";
        public decimal? Total { get; set; }
        public DateTime InceptionDate { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? ValidUntil { get; set; }
        public Guid? RenewalOfPolicyId { get; set; }
    }

    private sealed class ReferralRow
    {
        public Guid QuoteId { get; set; }
        public string Reference { get; set; } = "";
        public string AgencyCode { get; set; } = "";
        public string InsuredName { get; set; } = "";
        public string TradeCode { get; set; } = "";
        public decimal? Total { get; set; }
        public int ReferralCount { get; set; }
        public DateTime? ReferredUtc { get; set; }
        public DateTime InceptionDate { get; set; }
        public string? AssignedTo { get; set; }
    }
}

public sealed class BordereauLine
{
    public Guid TransactionId { get; set; }
    public string PolicyNumber { get; set; } = "";
    public string AgencyCode { get; set; } = "";
    public string AgencyName { get; set; } = "";
    public string InsuredName { get; set; } = "";
    public string Postcode { get; set; } = "";
    public string TradeCode { get; set; } = "";
    public DateTime InceptionDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string TransactionType { get; set; } = "";
    public DateTime EffectiveDate { get; set; }
    public decimal GrossPremium { get; set; }
    public decimal Ipt { get; set; }
    public decimal Commission { get; set; }
    public decimal Fee { get; set; }
    public string SectionGrossJson { get; set; } = "{}";
    public DateTime CreatedUtc { get; set; }
    public string ProductVersion { get; set; } = "";
}

internal sealed class GuidHandler : SqlMapper.TypeHandler<Guid>
{
    public override Guid Parse(object value) => value is Guid guid ? guid : Guid.Parse(Convert.ToString(value)!);

    public override void SetValue(IDbDataParameter parameter, Guid value) => parameter.Value = value;
}

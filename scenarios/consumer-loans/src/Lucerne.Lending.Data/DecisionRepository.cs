using System.Data;

namespace Lucerne.Lending.Data;

public sealed class DecisionRepository
{
    private readonly Db _db;

    public DecisionRepository(Db db) => _db = db;

    public Task AddBureauReportAsync(BureauReportRow r, IDbTransaction? tx = null) =>
        _db.ExecuteAsync(@"INSERT INTO lending.BureauReport(ApplicantId, Provider, RequestId, Score, PaymentDefault, DefaultCount, DebtRestructuring,
                CreditBlock, ReportedLoans, RawXml, CreatedUtc)
            VALUES (@ApplicantId, @Provider, @RequestId, @Score, @PaymentDefault, @DefaultCount, @DebtRestructuring, @CreditBlock, @ReportedLoans,
                @RawXml, @CreatedUtc)", r, tx);

    /// <summary>Latest report per applicant not older than <paramref name="maxAge"/> (reports are reused within 30 days).</summary>
    public Task<BureauReportRow?> RecentBureauReportAsync(string personalIdentityCode, TimeSpan maxAge, CancellationToken ct = default) =>
        _db.FirstOrDefaultAsync<BureauReportRow>(@"SELECT TOP (1) b.* FROM lending.BureauReport b
                JOIN lending.Applicant p ON p.Id = b.ApplicantId
            WHERE p.PersonalIdentityCode = @pic AND b.CreatedUtc >= @since ORDER BY b.Id DESC",
            new { pic = personalIdentityCode.ToUpperInvariant(), since = DateTime.UtcNow - maxAge }, ct: ct);

    public Task<IReadOnlyList<BureauReportRow>> BureauReportsAsync(long applicationId, CancellationToken ct = default) =>
        _db.QueryAsync<BureauReportRow>(@"SELECT b.* FROM lending.BureauReport b JOIN lending.Applicant p ON p.Id = b.ApplicantId
            WHERE p.ApplicationId = @applicationId ORDER BY b.Id", new { applicationId }, ct: ct);

    public Task AddAsync(DecisionRow d, IDbTransaction? tx = null) =>
        _db.ExecuteAsync(@"INSERT INTO lending.Decision(ApplicationId, Outcome, RiskGrade, Score, ReasonCodes, ScorecardJson, RuleTraceJson,
                AffordabilityJson, PolicyVersion, DecidedBy, IsOverride, Comment, CreatedUtc)
            VALUES (@ApplicationId, @Outcome, @RiskGrade, @Score, @ReasonCodes, @ScorecardJson, @RuleTraceJson, @AffordabilityJson,
                @PolicyVersion, @DecidedBy, @IsOverride, @Comment, @CreatedUtc)", d, tx);

    public Task<DecisionRow?> LatestAsync(long applicationId, CancellationToken ct = default) =>
        _db.FirstOrDefaultAsync<DecisionRow>("SELECT TOP (1) * FROM lending.Decision WHERE ApplicationId = @applicationId ORDER BY Id DESC",
            new { applicationId }, ct: ct);

    public Task<IReadOnlyList<DecisionRow>> ListAsync(long applicationId, CancellationToken ct = default) =>
        _db.QueryAsync<DecisionRow>("SELECT * FROM lending.Decision WHERE ApplicationId = @applicationId ORDER BY Id", new { applicationId }, ct: ct);

    public Task AddOfferAsync(OfferRow o, IDbTransaction? tx = null) =>
        _db.ExecuteAsync(@"INSERT INTO lending.Offer(ApplicationId, Amount, TermMonths, ReferenceRateCode, ReferenceRatePercent, MarginPercent,
                NominalRatePercent, AprPercent, MonthlyPayment, MonthlyFee, ArrangementFee, TotalRepayable, ValidUntilUtc, Status, CreatedUtc)
            VALUES (@ApplicationId, @Amount, @TermMonths, @ReferenceRateCode, @ReferenceRatePercent, @MarginPercent, @NominalRatePercent,
                @AprPercent, @MonthlyPayment, @MonthlyFee, @ArrangementFee, @TotalRepayable, @ValidUntilUtc, @Status, @CreatedUtc)", o, tx);

    public Task<OfferRow?> CurrentOfferAsync(long applicationId, CancellationToken ct = default) =>
        _db.FirstOrDefaultAsync<OfferRow>(@"SELECT TOP (1) * FROM lending.Offer WHERE ApplicationId = @applicationId
            AND Status IN ('Open', 'Accepted') ORDER BY Id DESC", new { applicationId }, ct: ct);

    public Task<int> WithdrawOpenOffersAsync(long applicationId, IDbTransaction? tx = null) =>
        _db.ExecuteAsync("UPDATE lending.Offer SET Status = 'Withdrawn' WHERE ApplicationId = @applicationId AND Status = 'Open'", new { applicationId }, tx);

    public Task<int> AcceptOfferAsync(long offerId, IDbTransaction? tx = null) =>
        _db.ExecuteAsync("UPDATE lending.Offer SET Status = 'Accepted', AcceptedUtc = @now WHERE Id = @offerId AND Status = 'Open'",
            new { offerId, now = DateTime.UtcNow }, tx);

    public Task<IReadOnlyList<OfferRow>> ExpiredOpenOffersAsync(DateTime nowUtc, CancellationToken ct = default) =>
        _db.QueryAsync<OfferRow>("SELECT * FROM lending.Offer WHERE Status = 'Open' AND ValidUntilUtc < @nowUtc", new { nowUtc }, ct: ct);

    public Task ExpireOfferAsync(long offerId, IDbTransaction? tx = null) =>
        _db.ExecuteAsync("UPDATE lending.Offer SET Status = 'Expired' WHERE Id = @offerId AND Status = 'Open'", new { offerId }, tx);
}

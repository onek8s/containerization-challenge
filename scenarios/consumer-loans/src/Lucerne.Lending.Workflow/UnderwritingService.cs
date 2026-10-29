using System.Text.Json;
using Lucerne.Lending.CoreBanking;
using Lucerne.Lending.Data;
using Lucerne.Lending.Domain;
using Microsoft.Extensions.Options;

namespace Lucerne.Lending.Workflow;

public sealed record ManualDecision(
    bool Approve,
    RiskGrade? Grade,
    decimal? Amount,
    int? TermMonths,
    decimal? MarginOverridePercent,
    string Comment);

/// <summary>Back office decisions on referred applications.</summary>
public sealed class UnderwritingService
{
    private readonly ApplicationRepository _applications;
    private readonly DecisionRepository _decisions;
    private readonly ProductRepository _products;
    private readonly OutboxRepository _outbox;
    private readonly ICoreReferenceData _referenceData;
    private readonly Db _db;
    private readonly LendingOptions _options;

    public UnderwritingService(ApplicationRepository applications, DecisionRepository decisions, ProductRepository products,
        OutboxRepository outbox, ICoreReferenceData referenceData, Db db, IOptions<LendingOptions> options)
    {
        _applications = applications;
        _decisions = decisions;
        _products = products;
        _outbox = outbox;
        _referenceData = referenceData;
        _db = db;
        _options = options.Value;
    }

    public bool NeedsSenior(ManualDecision d, decimal amount) =>
        d.Approve && ((d.Amount ?? amount) > _options.SeniorApprovalLimit || d.MarginOverridePercent.HasValue);

    public async Task DecideAsync(string reference, ManualDecision decision, string user, bool isSenior, int expectedVersion, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(decision.Comment)) throw new ValidationFailedException(new[] { "A comment is required for manual decisions." });
        var row = await _applications.GetRowAsync(reference, ct) ?? throw new KeyNotFoundException(reference);
        if (row.StatusValue != ApplicationStatus.Referred) throw new ConflictException($"Application is {row.StatusValue}, not referred.");
        if (row.Version != expectedVersion) throw new ConflictException("The application was changed by someone else. Reload and try again.");
        if (NeedsSenior(decision, row.Amount) && !isSenior)
            throw new UnauthorizedAccessException("Approvals over the limit or with a price override need a senior underwriter.");
        var product = (await _products.GetAsync(row.ProductCode, ct))!;
        var amount = decision.Amount ?? row.Amount;
        var term = decision.TermMonths ?? row.TermMonths;
        var errors = product.Validate(amount, term, (LoanPurpose)row.Purpose).ToList();
        if (errors.Count > 0) throw new ValidationFailedException(errors);
        var previous = await _decisions.LatestAsync(row.Id, ct);
        var grade = decision.Grade ?? (previous != null && Enum.TryParse<RiskGrade>(previous.RiskGrade, out var g) && g != RiskGrade.X ? g : RiskGrade.C);
        var refCode = ScoringService.ReferenceRateCode(product.ReferenceRate);
        var refRate = await _referenceData.GetReferenceRateAsync(refCode, await _referenceData.GetBusinessDateAsync(ct), ct);

        await _db.InTransactionAsync(async tx =>
        {
            await _decisions.AddAsync(new DecisionRow
            {
                ApplicationId = row.Id,
                Outcome = decision.Approve ? "Approve" : "Decline",
                RiskGrade = decision.Approve ? grade.ToString() : "X",
                Score = previous?.Score ?? 0,
                ReasonCodes = decision.Approve ? "UW01" : "UW02",
                ScorecardJson = previous?.ScorecardJson ?? "[]",
                RuleTraceJson = JsonSerializer.Serialize(new { decision.Amount, decision.TermMonths, decision.MarginOverridePercent }),
                AffordabilityJson = previous?.AffordabilityJson ?? "{}",
                PolicyVersion = previous?.PolicyVersion ?? "",
                DecidedBy = user,
                IsOverride = decision.MarginOverridePercent.HasValue || decision.Amount.HasValue || decision.TermMonths.HasValue,
                Comment = decision.Comment,
                CreatedUtc = DateTime.UtcNow
            }, tx);
            if (amount != row.Amount || term != row.TermMonths)
                await _db.ExecuteAsync("UPDATE lending.Application SET Amount = @amount, TermMonths = @term WHERE Id = @id",
                    new { amount, term, id = row.Id }, tx);
            var target = decision.Approve ? ApplicationStatus.Approved : ApplicationStatus.Declined;
            if (!await _applications.TransitionAsync(row.Id, ApplicationStatus.Referred, target, row.Version, user, decision.Comment, tx))
                throw new ConflictException("The application was changed by someone else. Reload and try again.");
            if (decision.Approve)
                await OfferIssuer.IssueAsync(_applications, _decisions, _outbox, row.Id, row.Version + 1, product, amount, term, grade, refCode, refRate,
                    decision.MarginOverridePercent, _options, user, tx);
            else
                await _outbox.EnqueueAsync(Topics.DocumentRequested, new DocumentRequest(DocumentKinds.DeclineLetter, row.Id, null), tx);
            return true;
        }, ct);
    }
}

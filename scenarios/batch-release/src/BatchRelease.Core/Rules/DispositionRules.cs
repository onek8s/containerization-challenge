using BatchRelease.Core.Model;

namespace BatchRelease.Core.Rules;

/// <summary>
/// Checks that a batch can be reviewed, released or rejected. The rules follow
/// SOP QA-0412 "Batch certification and release", revision 9.
/// </summary>
public sealed class DispositionRules
{
    private readonly TimeProvider _clock;

    public DispositionRules(TimeProvider clock) => _clock = clock;

    public Assessment Assess(Batch batch, DecisionKind kind, string signerSid)
    {
        ArgumentNullException.ThrowIfNull(batch);
        var findings = new List<Finding>();

        if (batch.Status is BatchStatus.Released or BatchStatus.Rejected)
        {
            findings.Add(Finding.Block("BR001", $"Batch {batch.BatchNumber} already has a final disposition ({batch.Status})."));
            return new Assessment(findings);
        }

        switch (kind)
        {
            case DecisionKind.Review:
                CheckRecord(batch, findings);
                CheckResults(batch, findings);
                CheckDeviations(batch, findings, forRelease: false);
                if (batch.Decisions.Any(d => d.Kind == DecisionKind.Review))
                    findings.Add(Finding.Block("BR002", "The batch record has already been reviewed."));
                break;

            case DecisionKind.Release:
                CheckRecord(batch, findings);
                CheckResults(batch, findings);
                CheckDeviations(batch, findings, forRelease: true);
                CheckShelfLife(batch, findings);
                CheckDocuments(batch, findings);
                CheckReview(batch, signerSid, findings);
                break;

            case DecisionKind.Reject:
                // A batch can always be rejected; the reason is required by the caller.
                break;

            case DecisionKind.Hold:
                if (batch.Status == BatchStatus.OnHold)
                    findings.Add(Finding.Block("BR003", "The batch is already on hold."));
                break;

            case DecisionKind.Resume:
                if (batch.Status != BatchStatus.OnHold)
                    findings.Add(Finding.Block("BR004", "The batch is not on hold."));
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }

        return new Assessment(findings);
    }

    private static void CheckRecord(Batch batch, List<Finding> findings)
    {
        if (batch.Steps.Count == 0)
        {
            findings.Add(Finding.Block("BR010", "The executed batch record has no steps."));
            return;
        }

        foreach (var step in batch.Steps.OrderBy(s => s.Sequence))
        {
            if (step.Status == StepStatus.Open)
            {
                findings.Add(Finding.Block("BR011", $"Step {step.Sequence} '{step.Operation}' is not complete."));
                continue;
            }

            if (step.Status == StepStatus.Complete)
            {
                if (string.IsNullOrWhiteSpace(step.PerformedBy) || string.IsNullOrWhiteSpace(step.VerifiedBy))
                    findings.Add(Finding.Block("BR012", $"Step {step.Sequence} '{step.Operation}' is missing the performer or the second-person verification."));
                else if (string.Equals(step.PerformedBy, step.VerifiedBy, StringComparison.OrdinalIgnoreCase))
                    findings.Add(Finding.Block("BR013", $"Step {step.Sequence} '{step.Operation}' was verified by the person who performed it."));
            }

            if (step.Status == StepStatus.NotApplicable && string.IsNullOrWhiteSpace(step.Comment))
                findings.Add(Finding.Warn("BR014", $"Step {step.Sequence} '{step.Operation}' is marked not applicable without a justification."));
        }
    }

    private static void CheckResults(Batch batch, List<Finding> findings)
    {
        if (batch.Results.Count == 0)
        {
            findings.Add(Finding.Block("BR020", "No laboratory results have been received."));
            return;
        }

        foreach (var result in batch.Results)
        {
            var outcome = SpecificationEvaluator.Evaluate(result);
            switch (outcome)
            {
                case ResultOutcome.Pending:
                    findings.Add(Finding.Block("BR021", $"Result {result.TestCode} '{result.TestName}' is pending."));
                    break;

                case ResultOutcome.Fail when string.IsNullOrEmpty(result.InvestigationNumber):
                    findings.Add(Finding.Block("BR022", $"Result {result.TestCode} is out of specification and has no investigation."));
                    break;

                case ResultOutcome.Fail when !result.InvestigationClosed:
                    findings.Add(Finding.Block("BR023", $"Result {result.TestCode} is out of specification; investigation {result.InvestigationNumber} is open."));
                    break;

                case ResultOutcome.Fail:
                    findings.Add(Finding.Block("BR024", $"Result {result.TestCode} is out of specification (investigation {result.InvestigationNumber} closed). The batch cannot be released on this result."));
                    break;
            }

            if (outcome != ResultOutcome.Pending && string.IsNullOrWhiteSpace(result.ApprovedBy))
                findings.Add(Finding.Block("BR025", $"Result {result.TestCode} has not been approved in the laboratory."));
        }
    }

    private static void CheckDeviations(Batch batch, List<Finding> findings, bool forRelease)
    {
        foreach (var deviation in batch.Deviations)
        {
            if (deviation.Impact == ProductImpact.Confirmed)
            {
                findings.Add(Finding.Block("BR030", $"Deviation {deviation.Number} has a confirmed product impact."));
                continue;
            }

            switch (deviation.Classification)
            {
                case DeviationClass.Critical when deviation.Status != DeviationStatus.Closed:
                case DeviationClass.Major when deviation.Status != DeviationStatus.Closed:
                    findings.Add(Finding.Block("BR031", $"{deviation.Classification} deviation {deviation.Number} is not closed."));
                    break;

                case DeviationClass.Minor when deviation.Status == DeviationStatus.Open:
                    findings.Add(forRelease
                        ? Finding.Block("BR032", $"Minor deviation {deviation.Number} has no impact assessment.")
                        : Finding.Warn("BR032", $"Minor deviation {deviation.Number} has no impact assessment yet."));
                    break;

                case DeviationClass.Minor when deviation.Status == DeviationStatus.ImpactAssessed:
                    findings.Add(Finding.Warn("BR033", $"Minor deviation {deviation.Number} is open with impact {deviation.Impact}."));
                    break;
            }

            if (deviation.Impact == ProductImpact.Possible && forRelease && deviation.Classification != DeviationClass.Minor)
                findings.Add(Finding.Block("BR034", $"Deviation {deviation.Number} has a possible product impact."));

            if (deviation.Classification != DeviationClass.Minor && string.IsNullOrEmpty(deviation.CapaNumber))
                findings.Add(Finding.Warn("BR035", $"Deviation {deviation.Number} has no CAPA reference."));
        }
    }

    private void CheckShelfLife(Batch batch, List<Finding> findings)
    {
        var product = batch.Product;
        if (product is null)
        {
            findings.Add(Finding.Block("BR040", $"Product {batch.ProductCode} is not registered."));
            return;
        }

        var expected = ShelfLife.ExpiryDate(batch.ManufacturedOn, product.ShelfLifeMonths);
        if (batch.ExpiresOn > expected)
            findings.Add(Finding.Block("BR041", $"Expiry date {batch.ExpiresOn:yyyy-MM-dd} is later than the registered shelf life allows ({expected:yyyy-MM-dd})."));

        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var remaining = ShelfLife.RemainingMonths(today, batch.ExpiresOn);
        if (remaining < product.MinimumRemainingMonths)
            findings.Add(Finding.Block("BR042", $"Only {remaining} months of shelf life remain; {product.MinimumRemainingMonths} are required at release."));
    }

    private static void CheckDocuments(Batch batch, List<Finding> findings)
    {
        foreach (var kind in RequiredDocuments)
        {
            if (!batch.Documents.Any(d => string.Equals(d.Kind, kind, StringComparison.OrdinalIgnoreCase)))
                findings.Add(Finding.Block("BR050", $"Document '{kind}' is missing from the batch file."));
        }
    }

    private static void CheckReview(Batch batch, string signerSid, List<Finding> findings)
    {
        var review = batch.Decisions.FirstOrDefault(d => d.Kind == DecisionKind.Review);
        if (review is null)
        {
            findings.Add(Finding.Block("BR060", "The batch record has not been reviewed by QA."));
            return;
        }

        if (string.Equals(review.SignerSid, signerSid, StringComparison.OrdinalIgnoreCase))
            findings.Add(Finding.Block("BR061", "The batch record was reviewed by the same person; release requires a second person."));

        if (batch.Status == BatchStatus.OnHold)
            findings.Add(Finding.Block("BR062", "The batch is on hold."));
    }

    public static readonly IReadOnlyList<string> RequiredDocuments = new[] { "ExecutedBatchRecord", "CertificateOfAnalysis" };
}

public static class StatusTransitions
{
    /// <summary>Status of the batch after a decision has been signed.</summary>
    public static BatchStatus After(Batch batch, DecisionKind kind) => kind switch
    {
        DecisionKind.Review => BatchStatus.Reviewed,
        DecisionKind.Release => BatchStatus.Released,
        DecisionKind.Reject => BatchStatus.Rejected,
        DecisionKind.Hold => BatchStatus.OnHold,
        DecisionKind.Resume => batch.Decisions.Any(d => d.Kind == DecisionKind.Review) ? BatchStatus.Reviewed : BatchStatus.UnderReview,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>A batch moves to review once the documents required for release are in the batch file.</summary>
    public static bool ReadyForReview(Batch batch) =>
        batch.Status == BatchStatus.Received &&
        DispositionRules.RequiredDocuments.All(k => batch.Documents.Any(d => string.Equals(d.Kind, k, StringComparison.OrdinalIgnoreCase)));
}

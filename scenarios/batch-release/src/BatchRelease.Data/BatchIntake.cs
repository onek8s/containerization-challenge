using BatchRelease.Core.Intake;
using BatchRelease.Core.Model;
using BatchRelease.Core.Rules;
using Microsoft.EntityFrameworkCore;

namespace BatchRelease.Data;

public enum IntakeOutcome
{
    Created,
    Updated,
    Rejected,
}

public sealed record IntakeResult(IntakeOutcome Outcome, int? BatchId, IReadOnlyList<string> Errors)
{
    public static IntakeResult Fail(params string[] errors) => new(IntakeOutcome.Rejected, null, errors);
}

/// <summary>
/// Stores batch submissions from the MES. A batch can be resubmitted (new lab results,
/// closed deviations) until it has been reviewed; after that the record is frozen.
/// </summary>
public sealed class BatchIntake
{
    private readonly BatchReleaseContext _db;
    private readonly TimeProvider _clock;

    public BatchIntake(BatchReleaseContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IntakeResult> SubmitAsync(BatchSubmission submission, string? clientSite, CancellationToken ct = default)
    {
        var errors = BatchSubmissionValidator.Validate(submission);
        if (errors.Count > 0)
            return new IntakeResult(IntakeOutcome.Rejected, null, errors);

        if (clientSite is not null && !string.Equals(clientSite, submission.SiteCode, StringComparison.OrdinalIgnoreCase))
            return IntakeResult.Fail($"This client may only submit batches for site {clientSite}.");

        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Code == submission.ProductCode, ct);
        if (product is null || !product.Active)
            return IntakeResult.Fail($"Product {submission.ProductCode} is not registered for release.");

        var batch = await _db.Batches
            .Include(b => b.Steps).Include(b => b.Results).Include(b => b.Deviations)
            .FirstOrDefaultAsync(b => b.BatchNumber == submission.BatchNumber, ct);

        var outcome = IntakeOutcome.Updated;
        if (batch is null)
        {
            if (await _db.Batches.AnyAsync(b => b.MesReference == submission.MesReference, ct))
                return IntakeResult.Fail($"MES reference {submission.MesReference} belongs to another batch.");

            batch = new Batch
            {
                BatchNumber = submission.BatchNumber,
                MesReference = submission.MesReference,
                ReceivedUtc = _clock.GetUtcNow().UtcDateTime,
                Status = BatchStatus.Received,
            };
            _db.Batches.Add(batch);
            outcome = IntakeOutcome.Created;
        }
        else
        {
            if (!string.Equals(batch.MesReference, submission.MesReference, StringComparison.Ordinal))
                return IntakeResult.Fail($"Batch {batch.BatchNumber} was submitted with MES reference {batch.MesReference}.");
            if (batch.Status is not (BatchStatus.Received or BatchStatus.UnderReview or BatchStatus.OnHold))
                return IntakeResult.Fail($"Batch {batch.BatchNumber} is {batch.Status}; the record can no longer be changed.");

            _db.RecordSteps.RemoveRange(batch.Steps);
            _db.LabResults.RemoveRange(batch.Results);
            _db.Deviations.RemoveRange(batch.Deviations);
            batch.Steps.Clear();
            batch.Results.Clear();
            batch.Deviations.Clear();
        }

        batch.ProductCode = product.Code;
        batch.SiteCode = submission.SiteCode.ToUpperInvariant();
        batch.ManufacturedOn = submission.ManufacturedOn;
        batch.ExpiresOn = submission.ExpiresOn ?? ShelfLife.ExpiryDate(submission.ManufacturedOn, product.ShelfLifeMonths);
        batch.Quantity = submission.Quantity;
        batch.Unit = submission.Unit;

        batch.Steps.AddRange(submission.Steps.Select(s => new RecordStep
        {
            Sequence = s.Sequence,
            Operation = s.Operation,
            Status = s.Status,
            PerformedBy = s.PerformedBy,
            VerifiedBy = s.VerifiedBy,
            CompletedUtc = s.CompletedUtc,
            Comment = s.Comment,
        }));
        batch.Results.AddRange(submission.Results.Select(r =>
        {
            var result = new LabResult
            {
                TestCode = r.TestCode,
                TestName = r.TestName,
                Specification = r.Specification,
                LowerLimit = r.LowerLimit,
                UpperLimit = r.UpperLimit,
                Value = r.Value,
                TextResult = r.TextResult,
                Unit = r.Unit,
                Outcome = r.Outcome,
                InvestigationNumber = r.InvestigationNumber,
                InvestigationClosed = r.InvestigationClosed,
                ApprovedBy = r.ApprovedBy,
            };
            result.Outcome = SpecificationEvaluator.Evaluate(result);
            return result;
        }));
        batch.Deviations.AddRange(submission.Deviations.Select(d => new Deviation
        {
            Number = d.Number,
            Title = d.Title,
            Classification = d.Classification,
            Status = d.Status,
            Impact = d.Impact,
            CapaNumber = d.CapaNumber,
        }));

        await _db.SaveChangesAsync(ct);
        return new IntakeResult(outcome, batch.Id, Array.Empty<string>());
    }
}

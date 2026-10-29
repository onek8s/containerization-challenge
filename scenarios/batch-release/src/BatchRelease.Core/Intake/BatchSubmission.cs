using BatchRelease.Core.Model;

namespace BatchRelease.Core.Intake;

/// <summary>Executed batch record as sent by the MES (and later updates from the LIMS and QMS).</summary>
public sealed class BatchSubmission
{
    public string MesReference { get; set; } = "";
    public string BatchNumber { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public string SiteCode { get; set; } = "";
    public DateOnly ManufacturedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "";
    public List<StepSubmission> Steps { get; set; } = new();
    public List<ResultSubmission> Results { get; set; } = new();
    public List<DeviationSubmission> Deviations { get; set; } = new();
}

public sealed class StepSubmission
{
    public int Sequence { get; set; }
    public string Operation { get; set; } = "";
    public StepStatus Status { get; set; }
    public string? PerformedBy { get; set; }
    public string? VerifiedBy { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public string? Comment { get; set; }
}

public sealed class ResultSubmission
{
    public string TestCode { get; set; } = "";
    public string TestName { get; set; } = "";
    public string Specification { get; set; } = "";
    public decimal? LowerLimit { get; set; }
    public decimal? UpperLimit { get; set; }
    public decimal? Value { get; set; }
    public string? TextResult { get; set; }
    public string Unit { get; set; } = "";
    public ResultOutcome Outcome { get; set; }
    public string? InvestigationNumber { get; set; }
    public bool InvestigationClosed { get; set; }
    public string? ApprovedBy { get; set; }
}

public sealed class DeviationSubmission
{
    public string Number { get; set; } = "";
    public string Title { get; set; } = "";
    public DeviationClass Classification { get; set; }
    public DeviationStatus Status { get; set; }
    public ProductImpact Impact { get; set; }
    public string? CapaNumber { get; set; }
}

public static class BatchSubmissionValidator
{
    public static IReadOnlyList<string> Validate(BatchSubmission s)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(s.MesReference))
            errors.Add("mesReference is required.");
        if (!BatchNumbers.IsValid(s.BatchNumber))
            errors.Add($"batchNumber '{s.BatchNumber}' is not a valid batch number.");
        if (string.IsNullOrWhiteSpace(s.ProductCode))
            errors.Add("productCode is required.");
        if (string.IsNullOrWhiteSpace(s.SiteCode) || s.SiteCode.Length > 6)
            errors.Add("siteCode is required (max 6 characters).");
        if (s.Quantity <= 0)
            errors.Add("quantity must be positive.");
        if (string.IsNullOrWhiteSpace(s.Unit))
            errors.Add("unit is required.");
        if (s.ExpiresOn is { } expiry && expiry <= s.ManufacturedOn)
            errors.Add("expiresOn must be after manufacturedOn.");

        var duplicateSteps = s.Steps.GroupBy(x => x.Sequence).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateSteps.Count > 0)
            errors.Add($"Duplicate step sequence: {string.Join(", ", duplicateSteps)}.");

        var duplicateTests = s.Results.GroupBy(x => x.TestCode, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateTests.Count > 0)
            errors.Add($"Duplicate test code: {string.Join(", ", duplicateTests)}.");

        foreach (var r in s.Results.Where(r => r.LowerLimit is { } lo && r.UpperLimit is { } hi && lo > hi))
            errors.Add($"Result {r.TestCode}: lower limit is above upper limit.");

        foreach (var d in s.Deviations.Where(d => string.IsNullOrWhiteSpace(d.Number)))
            errors.Add($"Deviation '{d.Title}' has no number.");

        return errors;
    }
}

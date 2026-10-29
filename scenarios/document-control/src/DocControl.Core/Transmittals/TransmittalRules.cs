using Litware.DocControl.Documents;

namespace Litware.DocControl.Transmittals;

public sealed record TransmittalLine(string DocumentNumber, string Revision, string Title, RevisionStatus Status, string ProjectCode);

public static class TransmittalRules
{
    public const int MaxLines = 250;

    /// <summary>Returns the problems that prevent issuing the given lines; empty when valid.</summary>
    public static IReadOnlyList<string> Validate(string project, TransmittalPurpose purpose, IReadOnlyCollection<TransmittalLine> lines)
    {
        var errors = new List<string>();
        if (lines.Count == 0) errors.Add("Select at least one document.");
        if (lines.Count > MaxLines) errors.Add($"A transmittal can contain at most {MaxLines} documents.");

        foreach (var dup in lines.GroupBy(l => l.DocumentNumber).Where(g => g.Count() > 1))
            errors.Add($"{dup.Key} is listed more than once.");

        foreach (var line in lines)
        {
            if (!string.Equals(line.ProjectCode, project, StringComparison.Ordinal))
                errors.Add($"{line.DocumentNumber} belongs to project {line.ProjectCode}.");

            var allowed = line.Status == RevisionStatus.Approved
                || (purpose == TransmittalPurpose.ForReview && line.Status == RevisionStatus.Submitted);
            if (!allowed)
                errors.Add($"{line.DocumentNumber} {line.Revision} is {line.Status.ToString().ToLowerInvariant()} and cannot be issued {purpose.Code()}.");

            if (purpose == TransmittalPurpose.ForConstruction && RevisionCode.TryParse(line.Revision, out var code) && !code.IsConstruction)
                errors.Add($"{line.DocumentNumber} {line.Revision} is a preliminary revision and cannot be issued for construction.");
        }

        return errors;
    }
}

namespace Litware.DocControl.Documents;

public static class RevisionRules
{
    /// <summary>
    /// Checks whether <paramref name="proposed"/> can be submitted for a document whose existing
    /// revisions are <paramref name="existing"/>. Returns null when allowed, otherwise the reason.
    /// </summary>
    public static string? CheckNewRevision(RevisionCode proposed, IEnumerable<(RevisionCode Code, RevisionStatus Status)> existing)
    {
        var list = existing.ToList();
        if (list.Count == 0)
        {
            return proposed == RevisionCode.FirstPreliminary || proposed == RevisionCode.FirstConstruction
                ? null
                : $"The first revision must be {RevisionCode.FirstPreliminary} or {RevisionCode.FirstConstruction}.";
        }

        if (list.Any(r => r.Code == proposed && r.Status != RevisionStatus.Rejected))
            return $"Revision {proposed} already exists.";

        if (list.Any(r => r.Status == RevisionStatus.Submitted))
            return "The previous revision is still waiting for review.";

        var highest = list.Where(r => r.Status != RevisionStatus.Rejected).Select(r => r.Code).DefaultIfEmpty().Max();
        if (highest == default)
        {
            // Only rejected revisions so far: the same code may be submitted again.
            var rejected = list.Max(r => r.Code);
            return proposed == rejected || proposed == RevisionCode.FirstPreliminary || proposed == RevisionCode.FirstConstruction
                ? null
                : $"Resubmit as {rejected}.";
        }

        if (highest.IsConstruction && proposed.IsPreliminary)
            return "A document issued for construction cannot go back to a preliminary revision.";

        if (proposed.Stage == highest.Stage)
        {
            return proposed == highest.NextInStage() ? null : $"The next revision is {highest.NextInStage()}.";
        }

        // P -> C
        return proposed == RevisionCode.FirstConstruction ? null : $"The first construction revision is {RevisionCode.FirstConstruction}.";
    }

    public static RevisionCode SuggestNext(IEnumerable<(RevisionCode Code, RevisionStatus Status)> existing)
    {
        var valid = existing.Where(r => r.Status != RevisionStatus.Rejected).Select(r => r.Code).ToList();
        return valid.Count == 0 ? RevisionCode.FirstPreliminary : valid.Max().NextInStage();
    }
}

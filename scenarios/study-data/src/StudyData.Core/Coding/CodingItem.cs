namespace StudyData.Core.Coding;

public enum CodingState
{
    Pending,
    AutoCoded,
    NeedsReview,
    Coded,
    QueryRaised
}

/// <summary>A verbatim term (adverse event, medication, medical history) waiting for or having a dictionary code.</summary>
public class CodingItem
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public int FormInstanceId { get; set; }
    public string FieldCode { get; set; } = "";
    public string Dictionary { get; set; } = "";
    public string Verbatim { get; set; } = "";
    public CodingState State { get; set; }

    /// <summary>Lowest level term / drug code chosen.</summary>
    public string? Code { get; set; }

    public string? Term { get; set; }

    /// <summary>Hierarchy path, for example PT|HLT|HLGT|SOC with codes.</summary>
    public string? Hierarchy { get; set; }

    public string? DictionaryVersion { get; set; }
    public int? Score { get; set; }
    public string? CodedBy { get; set; }
    public DateTime? CodedAt { get; set; }
}

public record CodingCandidate(string Code, string Term, string PreferredCode, string PreferredTerm, string Hierarchy, int Score);

public static class CodingRules
{
    /// <summary>Score at or above which an exact single candidate is accepted without review.</summary>
    public const int AutoAcceptScore = 100;

    public static string Normalise(string verbatim)
    {
        var chars = verbatim.Trim().ToUpperInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : ' ')
            .ToArray();
        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static CodingState Decide(IReadOnlyList<CodingCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return CodingState.NeedsReview;
        }

        var top = candidates[0];
        var ties = candidates.Count(c => c.Score == top.Score);
        return top.Score >= AutoAcceptScore && ties == 1 ? CodingState.AutoCoded : CodingState.NeedsReview;
    }
}

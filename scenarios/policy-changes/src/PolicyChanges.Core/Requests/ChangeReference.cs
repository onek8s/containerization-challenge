namespace PolicyChanges.Core.Requests;

/// <summary>Request references: <c>PC-yyMMdd-</c> and a running number for the day.</summary>
public static class ChangeReference
{
    public static string Prefix(DateOnly day) => $"PC-{day:yyMMdd}-";

    public static string Create(DateOnly day, int sequence) => Prefix(day) + sequence.ToString("D4");

    public static int NextSequence(string? lastReferenceOfDay)
    {
        if (string.IsNullOrEmpty(lastReferenceOfDay)) return 1;
        var dash = lastReferenceOfDay.LastIndexOf('-');
        return int.TryParse(lastReferenceOfDay[(dash + 1)..], out var n) ? n + 1 : 1;
    }
}

namespace PolicyChanges.Core.Requests;

public sealed class ChangeDraft
{
    public string PolicyNumber { get; set; } = "";
    public ChangeType ChangeType { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string Field(string name) =>
        Fields.FirstOrDefault(f => string.Equals(f.Key, name, StringComparison.OrdinalIgnoreCase)).Value?.Trim() ?? "";
}

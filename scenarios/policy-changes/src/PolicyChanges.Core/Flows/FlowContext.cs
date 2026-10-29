using PolicyChanges.Core.Requests;

namespace PolicyChanges.Core.Flows;

public sealed record FlowRequest(
    string Reference,
    string PolicyNumber,
    ChangeType ChangeType,
    DateOnly EffectiveDate,
    IReadOnlyDictionary<string, string> Fields)
{
    public string Field(string name) => Fields.TryGetValue(name, out var v) ? v.Trim() : "";
}

public sealed class FlowContext
{
    public FlowContext(FlowRequest request) => Request = request;

    public FlowRequest Request { get; }
    public Dictionary<string, string> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<FlowEvidence> Evidence { get; } = new();
    public IDesktopWindow? Window { get; set; }
    public string[]? Item { get; set; }
    public string? CurrentStep { get; set; }
}

public sealed record FlowEvidence(string Name, byte[] Png);

public enum FlowOutcome
{
    Applied,
    Rejected,
    Failed,
}

public sealed record FlowResult(
    FlowOutcome Outcome,
    string Message,
    IReadOnlyDictionary<string, string> Variables,
    IReadOnlyList<FlowEvidence> Evidence)
{
    /// <summary>Endorsement number reported by PolicyDesk.</summary>
    public string? Endorsement => Variables.TryGetValue("Endorsement", out var e) ? e : null;
}

/// <summary>PolicyDesk refused the change; retrying does not help.</summary>
public sealed class FlowRejectedException : Exception
{
    public FlowRejectedException(string message) : base(message) { }
}

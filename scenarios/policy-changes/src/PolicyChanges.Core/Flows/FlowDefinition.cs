using System.Text.Json;
using System.Text.Json.Serialization;
using PolicyChanges.Core.Requests;

namespace PolicyChanges.Core.Flows;

/// <summary>Screen flow that enters one change type in PolicyDesk. Stored as JSON in the <c>flows</c> folder.</summary>
public sealed class FlowDefinition
{
    public string Name { get; set; } = "";
    public ChangeType ChangeType { get; set; }

    /// <summary>PolicyDesk client version the flow was recorded against.</summary>
    public string ClientVersion { get; set; } = "";

    public int DefaultTimeoutSeconds { get; set; } = 15;
    public List<FlowStep> Steps { get; set; } = new();

    /// <summary>Run after a failure to bring the client back to the main window.</summary>
    public List<FlowStep> Cleanup { get; set; } = new();

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true,
    };

    public static FlowDefinition Parse(string json) =>
        JsonSerializer.Deserialize<FlowDefinition>(json, JsonOptions) ?? throw new FormatException("Empty flow definition.");
}

public enum FlowAction
{
    Window,
    Set,
    Select,
    Check,
    Click,
    Keys,
    Verify,
    Read,
    Capture,
    Dialog,
    Grid,
    ForEach,
    When,
    Pause,
}

public sealed class FlowStep
{
    public FlowAction Action { get; set; }

    /// <summary>Window title pattern (<see cref="FlowAction.Window"/>).</summary>
    public string? Title { get; set; }

    /// <summary>Label of the input or grid.</summary>
    public string? Field { get; set; }

    /// <summary>Value template, e.g. <c>{Field:Street}</c> or <c>{Effective}</c>.</summary>
    public string? Value { get; set; }

    /// <summary>Button text.</summary>
    public string? Button { get; set; }

    public string? Keys { get; set; }

    /// <summary>Variable that receives a read value or grid row count.</summary>
    public string? Store { get; set; }

    /// <summary>Evidence name for <see cref="FlowAction.Capture"/>.</summary>
    public string? Name { get; set; }

    /// <summary>Maps request values to the texts PolicyDesk shows in a list.</summary>
    public Dictionary<string, string>? Map { get; set; }

    /// <summary>Message box texts (regex) that mean success; named groups are stored as variables.</summary>
    public List<string>? Accept { get; set; }

    /// <summary>Message box texts (regex) that mean PolicyDesk refused the change.</summary>
    public List<string>? Reject { get; set; }

    /// <summary>The dialog does not always appear.</summary>
    public bool Optional { get; set; }

    /// <summary>Template that yields lines for <see cref="FlowAction.ForEach"/>; or the left side of <see cref="FlowAction.When"/>.</summary>
    public string? Items { get; set; }

    /// <summary>Right side of <see cref="FlowAction.When"/>.</summary>
    public string? Is { get; set; }

    /// <summary>Expected number of data rows (<see cref="FlowAction.Grid"/>).</summary>
    public string? ExpectRows { get; set; }

    public int? TimeoutSeconds { get; set; }
    public int? Milliseconds { get; set; }

    public List<FlowStep>? Steps { get; set; }
}

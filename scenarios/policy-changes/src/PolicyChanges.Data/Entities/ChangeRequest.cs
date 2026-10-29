using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using PolicyChanges.Core.Flows;
using PolicyChanges.Core.Requests;

namespace PolicyChanges.Data.Entities;

public class ChangeRequest
{
    public long Id { get; set; }
    public string Reference { get; set; } = "";
    public string PolicyNumber { get; set; } = "";
    public ChangeType ChangeType { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string FieldsJson { get; set; } = "{}";
    public ChangeStatus Status { get; set; }
    public ChangeChannel Channel { get; set; }
    public string SubmittedBy { get; set; } = "";
    public int? PartnerId { get; set; }
    public Partner? Partner { get; set; }
    public string? PartnerReference { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public int Attempts { get; set; }
    public DateTime NextAttemptUtc { get; set; }
    public string? LockedBy { get; set; }
    public DateTime? LockedUntilUtc { get; set; }

    /// <summary>Endorsement number from PolicyDesk.</summary>
    public string? Endorsement { get; set; }
    public string? ResultMessage { get; set; }
    public DateTime? CompletedUtc { get; set; }

    public List<ChangeEvent> Events { get; set; } = new();
    public List<ChangeEvidence> Evidence { get; set; } = new();

    [NotMapped]
    public Dictionary<string, string> Fields
    {
        get => JsonSerializer.Deserialize<Dictionary<string, string>>(FieldsJson) ?? new();
        set => FieldsJson = JsonSerializer.Serialize(value);
    }

    public FlowRequest ToFlowRequest() =>
        new(Reference, PolicyNumber, ChangeType, EffectiveDate, new Dictionary<string, string>(Fields, StringComparer.OrdinalIgnoreCase));
}

public class ChangeEvent
{
    public long Id { get; set; }
    public long ChangeRequestId { get; set; }
    public DateTime AtUtc { get; set; }
    public ChangeStatus Status { get; set; }
    public string Message { get; set; } = "";
    public string By { get; set; } = "";
}

/// <summary>Screenshot taken in PolicyDesk while the change was entered.</summary>
public class ChangeEvidence
{
    public long Id { get; set; }
    public long ChangeRequestId { get; set; }
    public int Attempt { get; set; }
    public string Name { get; set; } = "";

    /// <summary>Path below the evidence share.</summary>
    public string RelativePath { get; set; } = "";
    public DateTime CapturedUtc { get; set; }
    public string Station { get; set; } = "";
}

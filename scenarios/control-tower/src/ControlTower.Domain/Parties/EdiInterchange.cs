namespace Proseware.ControlTower.Domain.Parties;

public enum InterchangeDirection
{
    Inbound = 1,
    Outbound = 2
}

public enum InterchangeStatus
{
    Received = 0,
    Processed = 1,
    PartiallyProcessed = 2,
    Failed = 3,
    Sent = 4,
    Acknowledged = 5
}

/// <summary>Log of exchanged EDIFACT interchanges and API calls with carriers.</summary>
public class EdiInterchange
{
    public long Id { get; set; }
    public int CarrierId { get; set; }
    public InterchangeDirection Direction { get; set; }
    public string MessageType { get; set; } = "";
    public string ControlReference { get; set; } = "";
    public string FileName { get; set; } = "";
    public int MessageCount { get; set; }
    public InterchangeStatus Status { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? Payload { get; set; }
}

namespace ShiftClock.Shared;

public enum PunchDirection
{
    In,
    Out
}

public enum PunchSource
{
    Reader,
    Correction
}

public sealed record Punch
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string BadgeId { get; init; }
    public required string EmployeeNumber { get; init; }
    public required string EmployeeName { get; init; }
    public DateTime Timestamp { get; init; }
    public PunchDirection Direction { get; init; }
    public PunchSource Source { get; init; }
    public string? Terminal { get; init; }
    public string? CorrectedBy { get; init; }
    public string? Reason { get; init; }
}

public sealed record Employee(string BadgeId, string EmployeeNumber, string Name, string CostCenter);

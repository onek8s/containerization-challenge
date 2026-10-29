namespace ShiftClock.Service;

/// <summary>Live state of the badge reader shown on the supervisor console.</summary>
public sealed class ReaderState
{
    public DateTime StartedAt { get; } = DateTime.Now;
    public bool PortOpen { get; set; }
    public DateTime? LastBadgeAt { get; set; }
    public string? LastBadgeName { get; set; }
    public int RejectedFrames { get; set; }
}

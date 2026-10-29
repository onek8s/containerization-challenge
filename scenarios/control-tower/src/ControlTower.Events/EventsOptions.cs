namespace Proseware.ControlTower.Events;

public sealed class EventsOptions
{
    public const string Section = "Events";

    public int RuleSweepMinutes { get; set; } = 5;
    /// <summary>ETA changes below this are not published.</summary>
    public int EtaChangeThresholdMinutes { get; set; } = 30;
    /// <summary>Minimum interval between ETA recalculations per shipment.</summary>
    public int EtaRecalculateSeconds { get; set; } = 120;
    public int ScanLookbackHours { get; set; } = 24;
    public int StoppedBelowKmh { get; set; } = 3;
}

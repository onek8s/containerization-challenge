namespace FieldService.Worker;

public class WorkerOptions
{
    public const string Section = "Worker";

    public string InboxFolder { get; set; } = "";

    public string ArchiveFolder { get; set; } = "";

    public string ErrorFolder { get; set; } = "";

    public string OutboxFolder { get; set; } = "";

    public string ReportFolder { get; set; } = "";

    public string StateFolder { get; set; } = "";

    public int InboxPollSeconds { get; set; } = 60;

    public TimeSpan NightlyRunAt { get; set; } = new(2, 0, 0);

    public string TimeZone { get; set; } = "FLE Standard Time";
}

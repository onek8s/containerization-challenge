namespace StudyData.Monitor.Api.Services;

public class MonitorOptions
{
    public const string Section = "Monitor";

    public string Authority { get; set; } = "";
    public string ClientId { get; set; } = "";

    /// <summary>AD group (as sent by ADFS in the group claim) for each application role.</summary>
    public Dictionary<string, string> Groups { get; set; } = new();

    public double PageImageDpi { get; set; } = 110;
    public int NotificationPollSeconds { get; set; } = 5;

    /// <summary>Development only: the user signed in without ADFS.</summary>
    public string? DevelopmentUser { get; set; }
}

public static class Policies
{
    public const string Review = "Review";
    public const string DataManagement = "DataManagement";
    public const string Coding = "Coding";
}

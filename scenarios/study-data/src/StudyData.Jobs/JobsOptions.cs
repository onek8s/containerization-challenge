namespace StudyData.Jobs;

public class JobsOptions
{
    public const string Section = "Jobs";

    public string DocumentsCron { get; set; } = "0 * * * * ?";
    public string NightlyCasebooksCron { get; set; } = "0 30 1 * * ?";
    public string SafetyReportCron { get; set; } = "0 0 5 1 * ?";
    public string CodingCron { get; set; } = "0 0/15 * * * ?";
    public string LabImportCron { get; set; } = "0 10/30 * * * ?";
    public string QueryRemindersCron { get; set; } = "0 0 7 ? * MON-FRI";

    /// <summary>DSMB workbook template in the study's reports folder.</summary>
    public string SafetyTemplate { get; set; } = "";

    public string PortalUrl { get; set; } = "https://edc.corp.local/";
}

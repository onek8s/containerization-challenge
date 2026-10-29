namespace StaffOnboarding.Portal;

public sealed class PortalOptions
{
    public const string Section = "Portal";

    public string Domain { get; set; } = "CORP";
    public string HrGroup { get; set; } = "APP-Onboarding-HR";
    public string ItGroup { get; set; } = "APP-Onboarding-IT";

    /// <summary>Base address of the workflow host, for example http://onboarding-flow.corp.local:8085/StaffOnboarding/.</summary>
    public string WorkflowBaseAddress { get; set; } = "http://onboarding-flow.corp.local:8085/StaffOnboarding/";

    /// <summary>SPN of the workflow host service account for Kerberos.</summary>
    public string? WorkflowSpn { get; set; }

    /// <summary>Employment documents are stored per request below this folder.</summary>
    public string DocumentsShare { get; set; } = @"\\files01.corp.local\Onboarding$\Documents";

    public string AccessMapPath { get; set; } = @"\\files01.corp.local\Onboarding$\Config\access-map.xml";
    public string HolidaysPath { get; set; } = @"\\files01.corp.local\Onboarding$\Config\holidays.txt";

    public long MaxDocumentBytes { get; set; } = 20 * 1024 * 1024;
    public string[] AllowedDocumentExtensions { get; set; } = { ".pdf", ".docx", ".jpg", ".png" };
}

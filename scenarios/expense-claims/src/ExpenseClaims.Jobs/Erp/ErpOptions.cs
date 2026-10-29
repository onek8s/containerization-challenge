namespace ExpenseClaims.Jobs.Erp;

public sealed class ErpOptions
{
    public const string SectionName = "Erp";

    /// <summary>OData V4 endpoint of the Dynamics NAV service tier.</summary>
    public string ODataUrl { get; set; } = "http://nav01.corp.local:7048/DynamicsNAV110/ODataV4/";

    public string Company { get; set; } = "Lamna Healthcare Oy";

    /// <summary>Name of the general journal page published as a web service in NAV.</summary>
    public string JournalService { get; set; } = "ExpenseJournal";

    public string JournalTemplate { get; set; } = "GENERAL";
    public string JournalBatch { get; set; } = "EXPCLAIMS";

    /// <summary>Leave empty to authenticate as the application pool account (Windows authentication).</summary>
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? Domain { get; set; }

    public int MaxClaimsPerRun { get; set; } = 200;
}

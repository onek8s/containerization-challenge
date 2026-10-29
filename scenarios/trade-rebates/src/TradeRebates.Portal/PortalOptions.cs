namespace ContosoFoods.TradeRebates.Portal;

public sealed class PortalOptions
{
    /// <summary>Share folder where uploaded workbooks are saved. Must be readable by the SQL Server service account.</summary>
    public string WorkbookUploadFolder { get; set; } = "";

    /// <summary>Statements are only served from below this folder.</summary>
    public string StatementRoot { get; set; } = "";

    public long MaxUploadBytes { get; set; } = 20 * 1024 * 1024;
}

public sealed class AccessOptions
{
    public string[] Analysts { get; set; } = [];
    public string[] Approvers { get; set; } = [];
    public string[] Admins { get; set; } = [];
}

public static class Policies
{
    public const string Analyst = "Analyst";
    public const string Approver = "Approver";
    public const string Admin = "Admin";
}

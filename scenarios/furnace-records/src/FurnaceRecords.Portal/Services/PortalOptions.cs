namespace FurnaceRecords.Portal.Services;

public sealed class PortalOptions
{
    public const string Section = "Portal";

    /// <summary><c>Negotiate</c> or <c>Development</c>.</summary>
    public string Authentication { get; set; } = "Negotiate";

    public string OperatorsGroup { get; set; } = @"CORP\FR-Operators";
    public string QualityGroup { get; set; } = @"CORP\FR-Quality";
    public string AdminsGroup { get; set; } = @"CORP\FR-Admins";

    /// <summary>Acquisition service address for live readings.</summary>
    public string DaqAddress { get; set; } = "http://daq01.corp.local:5200";

    public string TimeZone { get; set; } = "W. Europe Standard Time";

    /// <summary>Development sign-in: user name and groups.</summary>
    public string DevelopmentUser { get; set; } = @"CORP\quality1";
    public List<string> DevelopmentGroups { get; set; } = new();
}

public static class Policies
{
    public const string Operator = "Operator";
    public const string Quality = "Quality";
    public const string Admin = "Admin";
}

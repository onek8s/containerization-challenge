namespace PolicyChanges.Portal.Services;

public sealed class PortalOptions
{
    public const string Section = "Portal";

    /// <summary><c>Negotiate</c> or <c>Development</c>.</summary>
    public string Authentication { get; set; } = "Negotiate";

    /// <summary>Customer service staff.</summary>
    public string UsersGroup { get; set; } = @"CORP\Policy Service";

    /// <summary>Service leads: review queue, withdraw, partner keys.</summary>
    public string LeadsGroup { get; set; } = @"CORP\Policy Service Leads";

    /// <summary>Share where the desk stations store PolicyDesk screenshots.</summary>
    public string EvidenceRoot { get; set; } = "";

    /// <summary>A desk station is shown offline when it has not reported for this long.</summary>
    public int StationOfflineMinutes { get; set; } = 3;
}

public static class Policies
{
    public const string Leads = "Leads";
}

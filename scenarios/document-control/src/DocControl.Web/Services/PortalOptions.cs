namespace Litware.DocControl.Web.Services;

public sealed class PortalOptions
{
    public const string SectionName = "DocControl:Portal";

    public string ControllersGroup { get; set; } = @"CORP\DocControl-Controllers";
    public string EngineersGroup { get; set; } = @"CORP\DocControl-Engineers";

    /// <summary>Format for the user's mail address; {0} is the account name without the domain.</summary>
    public string MailAddressFormat { get; set; } = "{0}@litware-eng.com";

    public long MaxUploadBytes { get; set; } = 500L * 1024 * 1024;

    public string? DefaultProject { get; set; }
}

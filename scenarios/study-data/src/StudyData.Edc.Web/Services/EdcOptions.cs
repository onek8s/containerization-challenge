namespace StudyData.Edc.Web.Services;

public class EdcOptions
{
    public const string Section = "Edc";

    /// <summary>Site staff must sign in with an authenticator app code.</summary>
    public bool RequireTwoFactor { get; set; } = true;

    public string SourceDocumentVault { get; set; } = "";

    public int MaxUploadMegabytes { get; set; } = 40;
}

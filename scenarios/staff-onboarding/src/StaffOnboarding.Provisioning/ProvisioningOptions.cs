namespace StaffOnboarding.Provisioning;

public sealed class ProvisioningOptions
{
    public const string Section = "Provisioning";

    /// <summary>DNS name of the AD domain.</summary>
    public string Domain { get; set; } = "corp.local";

    /// <summary>NetBIOS name used for NTFS permissions (CORP\user).</summary>
    public string NetBiosDomain { get; set; } = "CORP";

    /// <summary>
    /// Domain controller that all writes go to, so a step never reads an object another DC has not seen yet.
    /// Empty means locate a writable DC in the site at startup.
    /// </summary>
    public string? DomainController { get; set; }

    public string? Site { get; set; }
    public string UpnSuffix { get; set; } = "corp.local";
    public string MailDomain { get; set; } = "litware.com";
    public string Company { get; set; } = "Litware Inc.";

    /// <summary>DFS namespace folder for home folders, for example \\corp.local\dfs\home.</summary>
    public string HomeRoot { get; set; } = @"\\corp.local\dfs\home";

    public string HomeDrive { get; set; } = "H:";

    /// <summary>Home folders of leavers are moved here, under a year folder.</summary>
    public string ArchiveRoot { get; set; } = @"\\corp.local\dfs\archive\home";

    /// <summary>Group with full control on every home folder.</summary>
    public string FileAdminsGroup { get; set; } = @"CORP\FS-Home-Admins";

    /// <summary>Make the new user owner of the home folder (needs SeRestorePrivilege on the file servers).</summary>
    public bool SetHomeFolderOwner { get; set; } = true;

    /// <summary>Accounts that may call the API (the workflow host gMSA).</summary>
    public string[] AllowedCallers { get; set; } = Array.Empty<string>();

    /// <summary>Accounts that may only read (GET) accounts, for example the onboarding portal.</summary>
    public string[] ReadOnlyCallers { get; set; } = Array.Empty<string>();

    /// <summary>http.sys prefixes; http://+:8090/ when none are configured. (A non-empty default would be kept in front of the configured values.)</summary>
    public string[] UrlPrefixes { get; set; } = Array.Empty<string>();

    public string DisabledDescriptionFormat { get; set; } = "Disabled {0:yyyy-MM-dd} by {1}";
}

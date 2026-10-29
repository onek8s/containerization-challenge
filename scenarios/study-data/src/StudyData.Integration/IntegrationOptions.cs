namespace StudyData.Integration;

public class LabTransferOptions
{
    public const string Section = "CentralLab";

    /// <summary>Sftp or Folder.</summary>
    public string Source { get; set; } = "Sftp";

    public string Host { get; set; } = "sftp.centrallab.example";
    public int Port { get; set; } = 22;
    public string UserName { get; set; } = "fabrikam";

    /// <summary>Private key file (OpenSSH or PuTTY format) of the transfer account.</summary>
    public string PrivateKeyFile { get; set; } = @"C:\StudyData\Keys\centrallab_rsa";

    /// <summary>Expected host key fingerprint (SHA-256, base64); the transfer stops on a mismatch.</summary>
    public string HostKeySha256 { get; set; } = "";

    public string RemoteFolder { get; set; } = "/outbound/fabrikam";
    public string RemoteDoneFolder { get; set; } = "/outbound/fabrikam/done";

    /// <summary>Local folder used when Source is Folder, and where downloaded files are kept.</summary>
    public string LocalFolder { get; set; } = @"\\corp.local\dfs\StudyData\LabTransfers";
}

public class MailOptions
{
    public const string Section = "Mail";

    public string Host { get; set; } = "smtp.corp.local";
    public int Port { get; set; } = 25;
    public bool UseDefaultCredentials { get; set; } = true;
    public string From { get; set; } = "studydata@fabrikam-cr.example";

    /// <summary>When set, mails are written as .eml files here instead of being sent.</summary>
    public string? PickupFolder { get; set; }
}

public class ExportOptions
{
    public const string Section = "Exports";

    /// <summary>Exports, casebooks and safety reports: {root}\{study}\{kind}\...</summary>
    public string OutputRoot { get; set; } = @"\\corp.local\dfs\StudyData\Outputs";

    /// <summary>Password of the sponsor export archives (AES-256), per sponsor client id.</summary>
    public Dictionary<string, string> ArchivePasswords { get; set; } = new();
}

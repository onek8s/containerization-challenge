namespace Proseware.ControlTower.Integration.Sftp;

/// <summary>Connection to a partner's SFTP server (one entry per carrier in "Carriers:Sftp:&lt;code&gt;").</summary>
public sealed class SftpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 22;
    public string UserName { get; set; } = "";
    public string? Password { get; set; }
    /// <summary>OpenSSH/PEM private key file; used instead of the password when set.</summary>
    public string? PrivateKeyFile { get; set; }
    public string? PrivateKeyPassphrase { get; set; }
    /// <summary>Expected host key: "MD5:aa:bb:..." or "SHA256:base64". Empty accepts any key (test systems only).</summary>
    public string? HostKeyFingerprint { get; set; }
    public string InboundDirectory { get; set; } = "/out";
    public string ArchiveDirectory { get; set; } = "/out/archive";
    public string OutboundDirectory { get; set; } = "/in";
    public string FilePattern { get; set; } = "*.edi";
    public int TimeoutSeconds { get; set; } = 30;
    /// <summary>Local folder used instead of the server (lab and development use), with the same sub folders.</summary>
    public string? LocalFolder { get; set; }
}

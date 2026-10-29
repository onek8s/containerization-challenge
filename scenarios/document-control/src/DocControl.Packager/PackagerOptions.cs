namespace Litware.DocControl.Packager;

public sealed class PackagerOptions
{
    public const string SectionName = "DocControl:Packager";

    public int PollSeconds { get; set; } = 30;

    /// <summary>Concurrent package builds. 0 = half of the logical processors of the server.</summary>
    public int MaxParallelBuilds { get; set; }

    /// <summary>Local folder for packages being built. Empty = &lt;DataPath&gt;\work from the install registry key.</summary>
    public string? WorkFolder { get; set; }

    /// <summary>Builds are not started when the work folder or the transmittal share has less free space.</summary>
    public long MinFreeBytes { get; set; } = 5L * 1024 * 1024 * 1024;

    /// <summary>A build claimed longer ago than this is considered abandoned and claimed again.</summary>
    public int StaleMinutes { get; set; } = 60;

    public int MaxAttempts { get; set; } = 3;

    public string PortalUrl { get; set; } = "https://doccontrol.corp.local";

    public MailOptions Mail { get; set; } = new();
}

public sealed class MailOptions
{
    public string Host { get; set; } = "smtp.corp.local";
    public int Port { get; set; } = 25;
    public bool EnableSsl { get; set; }
    public bool UseDefaultCredentials { get; set; } = true;

    /// <summary>When set, mails are written to this folder instead of being sent (development).</summary>
    public string? PickupDirectory { get; set; }

    public string From { get; set; } = "doccontrol@litware-eng.com";
    public string FromName { get; set; } = "Litware Document Control";

    /// <summary>Packages up to this size are attached to the recipient mail.</summary>
    public long MaxAttachmentBytes { get; set; } = 15L * 1024 * 1024;
}

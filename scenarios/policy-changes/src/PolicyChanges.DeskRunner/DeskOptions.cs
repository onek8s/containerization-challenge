namespace PolicyChanges.DeskRunner;

public sealed class DeskOptions
{
    public const string Section = "Desk";

    /// <summary>Station name shown in the portal; defaults to the computer name.</summary>
    public string? StationName { get; set; }

    public int PollSeconds { get; set; } = 10;

    /// <summary>Wait when the desktop is not usable (locked, disconnected).</summary>
    public int DesktopWaitSeconds { get; set; } = 30;

    public string FlowsPath { get; set; } = "flows";

    /// <summary>Share for PolicyDesk screenshots (same as the portal's EvidenceRoot).</summary>
    public string EvidenceRoot { get; set; } = "";

    public string LogPath { get; set; } = @"%LOCALAPPDATA%\PolicyChanges\logs";
}

public sealed class PolicyDeskOptions
{
    public const string Section = "PolicyDesk";

    public string ExecutablePath { get; set; } = @"C:\Program Files (x86)\Halcyon\PolicyDesk 6\HPDESK.EXE";
    public string Arguments { get; set; } = "/env:{Environment} /nosplash";
    public string Environment { get; set; } = "PROD";

    /// <summary>Generic credential in Credential Manager of the desk user.</summary>
    public string CredentialTarget { get; set; } = "PolicyDesk/PROD";

    public string SignInTitle { get; set; } = "PolicyDesk - Sign in";
    public string MainWindowTitle { get; set; } = "PolicyDesk 6.* - *";
    public int StartupTimeoutSeconds { get; set; } = 90;

    /// <summary>PolicyDesk slows down over a long session; restart it after this many changes.</summary>
    public int RestartAfterChanges { get; set; } = 150;
}

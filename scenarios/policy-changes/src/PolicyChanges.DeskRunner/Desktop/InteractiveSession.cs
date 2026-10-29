using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using PolicyChanges.DeskRunner.Native;

namespace PolicyChanges.DeskRunner.Desktop;

public sealed record SessionState(bool Usable, string Reason, uint SessionId, string? InputDesktop, bool? Locked, bool Console);

/// <summary>
/// The desk runner works through the visible desktop of a signed-in user: windows must be shown,
/// keyboard input must reach them and the clipboard must belong to the session.
/// </summary>
internal static class InteractiveSession
{
    public static SessionState Check()
    {
        Kernel32.ProcessIdToSessionId((uint)Environment.ProcessId, out var sessionId);
        var console = Kernel32.WTSGetActiveConsoleSessionId() == sessionId;

        if (sessionId == 0)
            return new SessionState(false, "Running in session 0 (as a service or a scheduled task without a logged on user).", sessionId, null, null, console);

        var desktop = InputDesktopName();
        var locked = IsLocked(sessionId);

        if (desktop is null)
            return new SessionState(false, "The input desktop is not accessible (locked, secure desktop or disconnected).", sessionId, null, locked, console);
        if (!desktop.Equals("Default", StringComparison.OrdinalIgnoreCase))
            return new SessionState(false, $"The input desktop is '{desktop}', not the user desktop.", sessionId, desktop, locked, console);
        if (locked == true)
            return new SessionState(false, "The session is locked.", sessionId, desktop, locked, console);
        if (User32.SystemParametersInfo(User32.SPI_GETSCREENSAVERRUNNING, 0, out var saver, 0) && saver != 0)
            return new SessionState(false, "The screen saver is running.", sessionId, desktop, locked, console);

        return new SessionState(true, console ? "Console session" : "Remote desktop session", sessionId, desktop, locked, console);
    }

    private static string? InputDesktopName()
    {
        var handle = User32.OpenInputDesktop(0, false, User32.DESKTOP_READOBJECTS);
        if (handle == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(256);
            return User32.GetUserObjectInformation(handle, User32.UOI_NAME, sb, sb.Capacity * 2, out _) ? sb.ToString() : null;
        }
        finally
        {
            User32.CloseDesktop(handle);
        }
    }

    private static bool? IsLocked(uint sessionId)
    {
        if (!WtsApi32.WTSQuerySessionInformation(WtsApi32.WTS_CURRENT_SERVER_HANDLE, sessionId, WtsApi32.WTSSessionInfoEx, out var buffer, out _))
            return null;
        try
        {
            // WTSINFOEX: Level (4), padding to the 8-byte aligned WTSINFOEX_LEVEL1 (it contains LARGE_INTEGERs),
            // then SessionId (4), SessionState (4), SessionFlags (4). Offset 12 is SessionState (0 = active).
            var flags = Marshal.ReadInt32(buffer, 16);
            return flags switch
            {
                WtsApi32.WTS_SESSIONSTATE_LOCK => true,
                WtsApi32.WTS_SESSIONSTATE_UNLOCK => false,
                _ => null,
            };
        }
        finally
        {
            WtsApi32.WTSFreeMemory(buffer);
        }
    }

    /// <summary>Keeps the display on and the machine awake while the runner is active.</summary>
    public static void KeepAwake(bool on) =>
        Kernel32.SetThreadExecutionState(on
            ? Kernel32.ES_CONTINUOUS | Kernel32.ES_DISPLAY_REQUIRED | Kernel32.ES_SYSTEM_REQUIRED
            : Kernel32.ES_CONTINUOUS);

    public static int CurrentSessionId => Process.GetCurrentProcess().SessionId;
}

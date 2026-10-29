using PolicyChanges.Core.Flows;
using PolicyChanges.DeskRunner.Native;

namespace PolicyChanges.DeskRunner.Desktop;

/// <summary>Windows of one PolicyDesk process on the desk session's desktop.</summary>
internal sealed class Win32Desktop : IDesktopSession
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);
    private readonly Func<uint> _processId;

    public Win32Desktop(Func<uint> processId) => _processId = processId;

    public IDesktopWindow WaitForWindow(string titlePattern, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            var match = FindWindow(titlePattern);
            if (match != IntPtr.Zero) return new Win32Window(match);

            // A message box instead of the expected window means PolicyDesk refused something.
            var dialog = FindDialog();
            if (dialog != IntPtr.Zero)
                throw new DesktopException($"PolicyDesk shows '{new Win32Dialog(dialog).Text}' instead of '{titlePattern}'.");

            if (DateTime.UtcNow > deadline) throw new DesktopException($"Window '{titlePattern}' did not appear.");
            Thread.Sleep(PollInterval);
        }
    }

    public IDesktopDialog? WaitForDialog(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            var dialog = FindDialog();
            if (dialog != IntPtr.Zero)
            {
                Thread.Sleep(100); // let the text render
                return new Win32Dialog(dialog);
            }
            if (DateTime.UtcNow > deadline) return null;
            Thread.Sleep(PollInterval);
        }
    }

    public IntPtr FindWindow(string titlePattern) =>
        Win32.TopLevelWindows(_processId())
            .Where(h => Win32.ClassName(h) != Win32Dialog.DialogClass)
            .FirstOrDefault(h => Wildcard.IsMatch(Win32.Title(h), titlePattern));

    public IntPtr FindDialog() =>
        Win32.TopLevelWindows(_processId()).FirstOrDefault(h => Win32.ClassName(h) == Win32Dialog.DialogClass);

    /// <summary>Closes message boxes and every window except the main window.</summary>
    public int CloseOthers(IntPtr mainWindow)
    {
        var closed = 0;
        for (var round = 0; round < 5; round++)
        {
            var others = Win32.TopLevelWindows(_processId()).Where(h => h != mainWindow).ToList();
            if (others.Count == 0) return closed;
            foreach (var h in others)
            {
                if (Win32.ClassName(h) == Win32Dialog.DialogClass)
                {
                    var d = new Win32Dialog(h);
                    var button = d.Buttons.FirstOrDefault(b => b is "Cancel" or "No" or "OK" or "Close");
                    if (button is not null) d.Click(button);
                }
                else
                {
                    User32.PostMessage(h, User32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                }
                closed++;
            }
            Thread.Sleep(300);
        }
        return closed;
    }
}

using System.Text;
using PolicyChanges.Core.Flows;
using PolicyChanges.DeskRunner.Native;

namespace PolicyChanges.DeskRunner.Desktop;

/// <summary>Window helpers on top of user32.</summary>
internal static class Win32
{
    private const uint MessageTimeoutMs = 5000;

    public static string ClassName(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        User32.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>Title of a top-level window.</summary>
    public static string Title(IntPtr hwnd)
    {
        var length = User32.GetWindowTextLength(hwnd);
        var sb = new StringBuilder(length + 1);
        User32.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>Text of a control in another process (GetWindowText does not read those).</summary>
    public static string Text(IntPtr hwnd)
    {
        if (User32.SendMessageTimeout(hwnd, User32.WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero,
                User32.SMTO_ABORTIFHUNG, MessageTimeoutMs, out var length) == IntPtr.Zero)
            throw new DesktopException("PolicyDesk is not responding.");
        var sb = new StringBuilder((int)length + 1);
        User32.SendMessageTimeoutText(hwnd, User32.WM_GETTEXT, (IntPtr)sb.Capacity, sb,
            User32.SMTO_ABORTIFHUNG, MessageTimeoutMs, out _);
        return sb.ToString();
    }

    public static void SetText(IntPtr hwnd, string value) =>
        User32.SendMessageString(hwnd, User32.WM_SETTEXT, IntPtr.Zero, value);

    public static uint ProcessId(IntPtr hwnd)
    {
        User32.GetWindowThreadProcessId(hwnd, out var pid);
        return pid;
    }

    public static List<IntPtr> TopLevelWindows(uint processId)
    {
        var result = new List<IntPtr>();
        User32.EnumWindows((h, _) =>
        {
            if (User32.IsWindowVisible(h) && ProcessId(h) == processId) result.Add(h);
            return true;
        }, IntPtr.Zero);
        return result;
    }

    /// <summary>All descendants in z-order, the order PolicyDesk creates label and input pairs.</summary>
    public static List<IntPtr> Children(IntPtr parent)
    {
        var result = new List<IntPtr>();
        User32.EnumChildWindows(parent, (h, _) =>
        {
            result.Add(h);
            return true;
        }, IntPtr.Zero);
        return result;
    }

    public static string CleanLabel(string text) => text.Replace("&", "").Trim().TrimEnd(':').Trim();

    public static bool IsInput(string className) =>
        className.Contains("EDIT", StringComparison.OrdinalIgnoreCase)
        || className.Contains("COMBOBOX", StringComparison.OrdinalIgnoreCase)
        || className.Contains("ListView32", StringComparison.OrdinalIgnoreCase)
        || className.Contains("LISTBOX", StringComparison.OrdinalIgnoreCase)
        || className.Contains("RichEdit", StringComparison.OrdinalIgnoreCase);

    public static bool IsLabel(string className) => className.Contains("STATIC", StringComparison.OrdinalIgnoreCase);

    public static bool IsButton(string className) => className.Contains("BUTTON", StringComparison.OrdinalIgnoreCase);

    /// <summary>Brings the window to the foreground. Windows only lets the foreground process do this,
    /// so the call is made with the input queues attached and after an Alt keystroke.</summary>
    public static void Activate(IntPtr hwnd)
    {
        if (User32.GetForegroundWindow() == hwnd) return;
        if (User32.IsIconic(hwnd)) User32.ShowWindow(hwnd, User32.SW_RESTORE);

        var foreground = User32.GetForegroundWindow();
        var foregroundThread = User32.GetWindowThreadProcessId(foreground, out _);
        var thisThread = Kernel32.GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != thisThread
                       && User32.AttachThreadInput(thisThread, foregroundThread, true);
        try
        {
            Keyboard.TapAlt();
            User32.BringWindowToTop(hwnd);
            User32.SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached) User32.AttachThreadInput(thisThread, foregroundThread, false);
        }

        for (var i = 0; i < 20 && User32.GetForegroundWindow() != hwnd; i++) Thread.Sleep(50);
        if (User32.GetForegroundWindow() != hwnd)
            throw new DesktopException($"Could not activate '{Title(hwnd)}'. Is the desk session locked or minimised?");
    }

    /// <summary>Moves keyboard focus to a control of another process.</summary>
    public static void Focus(IntPtr top, IntPtr control)
    {
        Activate(top);
        var targetThread = User32.GetWindowThreadProcessId(control, out _);
        var thisThread = Kernel32.GetCurrentThreadId();
        if (!User32.AttachThreadInput(thisThread, targetThread, true))
            throw new DesktopException("Could not attach to the PolicyDesk input queue.");
        try
        {
            User32.SetFocus(control);
        }
        finally
        {
            User32.AttachThreadInput(thisThread, targetThread, false);
        }
    }
}

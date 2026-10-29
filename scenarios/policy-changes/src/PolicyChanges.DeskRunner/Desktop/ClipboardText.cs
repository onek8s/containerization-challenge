using System.Runtime.InteropServices;
using PolicyChanges.Core.Flows;
using PolicyChanges.DeskRunner.Native;

namespace PolicyChanges.DeskRunner.Desktop;

/// <summary>Reads text that PolicyDesk puts on the clipboard of the desk session.</summary>
internal static class ClipboardText
{
    public static uint Sequence => User32.GetClipboardSequenceNumber();

    public static string WaitForChange(uint previousSequence, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (User32.GetClipboardSequenceNumber() == previousSequence)
        {
            if (DateTime.UtcNow > deadline) throw new DesktopException("PolicyDesk did not copy anything to the clipboard.");
            Thread.Sleep(50);
        }
        return Read();
    }

    public static string Read()
    {
        for (var attempt = 0; ; attempt++)
        {
            if (User32.OpenClipboard(IntPtr.Zero)) break;
            if (attempt >= 20) throw new DesktopException("The clipboard is in use by another program.");
            Thread.Sleep(50);
        }
        try
        {
            if (!User32.IsClipboardFormatAvailable(User32.CF_UNICODETEXT)) return "";
            var handle = User32.GetClipboardData(User32.CF_UNICODETEXT);
            if (handle == IntPtr.Zero) return "";
            var ptr = Kernel32.GlobalLock(handle);
            try
            {
                return Marshal.PtrToStringUni(ptr) ?? "";
            }
            finally
            {
                Kernel32.GlobalUnlock(handle);
            }
        }
        finally
        {
            User32.CloseClipboard();
        }
    }
}

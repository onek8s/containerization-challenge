using PolicyChanges.Core.Flows;
using PolicyChanges.DeskRunner.Native;

namespace PolicyChanges.DeskRunner.Desktop;

/// <summary>Standard Windows message box shown by PolicyDesk.</summary>
internal sealed class Win32Dialog : IDesktopDialog
{
    // Message box text is in a static control with this id.
    private const int MessageTextId = 0xFFFF;

    private readonly IntPtr _handle;

    public Win32Dialog(IntPtr handle) => _handle = handle;

    public const string DialogClass = "#32770";

    public string Title => Win32.Title(_handle);

    public string Text
    {
        get
        {
            var children = Win32.Children(_handle);
            var message = children.FirstOrDefault(c => User32.GetDlgCtrlID(c) == MessageTextId && Win32.IsLabel(Win32.ClassName(c)));
            return message == IntPtr.Zero
                ? string.Join("\n", children.Where(c => Win32.IsLabel(Win32.ClassName(c))).Select(Win32.Text))
                : Win32.Text(message);
        }
    }

    public IReadOnlyList<string> Buttons =>
        Win32.Children(_handle)
            .Where(c => Win32.IsButton(Win32.ClassName(c)) && User32.IsWindowVisible(c))
            .Select(c => Win32.CleanLabel(Win32.Text(c)))
            .ToList();

    public void Click(string buttonText)
    {
        var wanted = Win32.CleanLabel(buttonText);
        var button = Win32.Children(_handle).FirstOrDefault(c =>
            Win32.IsButton(Win32.ClassName(c)) && string.Equals(Win32.CleanLabel(Win32.Text(c)), wanted, StringComparison.OrdinalIgnoreCase));
        if (button == IntPtr.Zero) throw new DesktopException($"Message '{Title}' has no button '{buttonText}'.");
        Win32.Activate(_handle);
        User32.PostMessage(button, User32.BM_CLICK, IntPtr.Zero, IntPtr.Zero);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (User32.IsWindow(_handle) && DateTime.UtcNow < deadline) Thread.Sleep(50);
    }

    public byte[] Capture() => WindowCapture.Png(_handle);
}

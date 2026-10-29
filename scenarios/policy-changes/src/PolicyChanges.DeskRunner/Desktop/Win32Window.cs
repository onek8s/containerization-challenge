using PolicyChanges.Core.Flows;
using PolicyChanges.DeskRunner.Native;

namespace PolicyChanges.DeskRunner.Desktop;

/// <summary>A PolicyDesk window. Inputs are found by the label in front of them.</summary>
internal sealed class Win32Window : IDesktopWindow
{
    private static readonly TimeSpan ClipboardTimeout = TimeSpan.FromSeconds(5);

    public Win32Window(IntPtr handle) => Handle = handle;

    public IntPtr Handle { get; }
    public string Title => Win32.Title(Handle);
    public bool IsOpen => User32.IsWindow(Handle) && User32.IsWindowVisible(Handle);

    public void SetText(string label, string value)
    {
        var input = FindInput(label);
        EnsureEnabled(input, label);
        Win32.SetText(input, value);
        // PolicyDesk validates on leaving the field; type a Tab from the field so the check runs.
        Win32.Focus(Handle, input);
        Keyboard.Send(KeySequence.Parse("{END}{TAB}"));
        // Password fields do not give their text to other processes.
        if ((User32.GetWindowLong(input, User32.GWL_STYLE) & User32.ES_PASSWORD) != 0) return;
        var shown = Win32.Text(input);
        if (!string.Equals(shown.Trim(), value.Trim(), StringComparison.Ordinal))
            throw new DesktopException($"{label} shows '{shown}' after entering '{value}'.");
    }

    public string GetText(string label) => Win32.Text(FindInput(label));

    public void Select(string label, string item)
    {
        var combo = FindInput(label);
        EnsureEnabled(combo, label);
        var index = User32.SendMessageString(combo, User32.CB_FINDSTRINGEXACT, new IntPtr(-1), item);
        if (index == new IntPtr(-1))
            throw new DesktopException($"{label} has no item '{item}'.");
        User32.SendMessage(combo, User32.CB_SETCURSEL, index, IntPtr.Zero);
        // CB_SETCURSEL does not notify the owner; send the notification a user selection would.
        var id = User32.GetDlgCtrlID(combo);
        var wParam = new IntPtr((User32.CBN_SELCHANGE << 16) | (id & 0xFFFF));
        User32.SendMessage(User32.GetParent(combo), User32.WM_COMMAND, wParam, combo);
    }

    public void SetChecked(string label, bool value)
    {
        var box = FindButton(label);
        var isChecked = User32.SendMessage(box, User32.BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == new IntPtr(1);
        if (isChecked != value) User32.SendMessage(box, User32.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
    }

    public void Click(string buttonText)
    {
        var button = FindButton(buttonText);
        EnsureEnabled(button, buttonText);
        Win32.Activate(Handle);
        // Posted: the button may open a modal dialog and SendMessage would block until it closes.
        User32.PostMessage(button, User32.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
        Thread.Sleep(150);
    }

    public void SendKeys(string keys)
    {
        Win32.Activate(Handle);
        Keyboard.Send(KeySequence.Parse(keys));
    }

    /// <summary>PolicyDesk grids copy their rows as tab separated text with Ctrl+Shift+C.</summary>
    public IReadOnlyList<IReadOnlyList<string>> CopyGrid(string label)
    {
        var grid = FindInput(label);
        var before = ClipboardText.Sequence;
        Win32.Focus(Handle, grid);
        Keyboard.Send(KeySequence.Parse("^+c"));
        var text = ClipboardText.WaitForChange(before, ClipboardTimeout);
        return text.Replace("\r", "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => (IReadOnlyList<string>)line.Split('\t'))
            .ToList();
    }

    public byte[] Capture() => WindowCapture.Png(Handle);

    private IntPtr FindInput(string label)
    {
        var children = Win32.Children(Handle).Where(User32.IsWindowVisible).ToList();
        var wanted = Win32.CleanLabel(label);
        for (var i = 0; i < children.Count; i++)
        {
            if (!Win32.IsLabel(Win32.ClassName(children[i]))) continue;
            if (!string.Equals(Win32.CleanLabel(Win32.Text(children[i])), wanted, StringComparison.OrdinalIgnoreCase)) continue;
            for (var j = i + 1; j < children.Count; j++)
            {
                var cls = Win32.ClassName(children[j]);
                if (Win32.IsInput(cls)) return children[j];
                if (Win32.IsLabel(cls)) break;
            }
        }
        throw new DesktopException($"No input labelled '{label}' in '{Title}'.");
    }

    private IntPtr FindButton(string text)
    {
        var wanted = Win32.CleanLabel(text);
        foreach (var child in Win32.Children(Handle))
        {
            if (!User32.IsWindowVisible(child) || !Win32.IsButton(Win32.ClassName(child))) continue;
            if (string.Equals(Win32.CleanLabel(Win32.Text(child)), wanted, StringComparison.OrdinalIgnoreCase)) return child;
        }
        throw new DesktopException($"No button '{text}' in '{Title}'.");
    }

    private static void EnsureEnabled(IntPtr hwnd, string name)
    {
        if (!User32.IsWindowEnabled(hwnd)) throw new DesktopException($"{name} is disabled.");
    }
}

namespace PolicyChanges.Core.Flows;

/// <summary>The PolicyDesk client as seen from the desk station.</summary>
public interface IDesktopSession
{
    /// <summary>Waits for a top-level PolicyDesk window whose title matches <paramref name="titlePattern"/> (<c>*</c> wildcards).</summary>
    IDesktopWindow WaitForWindow(string titlePattern, TimeSpan timeout);

    /// <summary>Waits for a PolicyDesk message box. Returns null when none appears in time.</summary>
    IDesktopDialog? WaitForDialog(TimeSpan timeout);
}

public interface IDesktopWindow
{
    string Title { get; }
    bool IsOpen { get; }

    /// <summary>Enters text into the input next to the label <paramref name="label"/>.</summary>
    void SetText(string label, string value);
    string GetText(string label);

    /// <summary>Selects an item of the list next to the label.</summary>
    void Select(string label, string item);

    void SetChecked(string label, bool value);
    void Click(string buttonText);

    /// <summary>Keystrokes in <see cref="KeySequence"/> notation.</summary>
    void SendKeys(string keys);

    /// <summary>Copies the grid next to the label (rows of cells, header row first).</summary>
    IReadOnlyList<IReadOnlyList<string>> CopyGrid(string label);

    /// <summary>PNG screenshot of the window.</summary>
    byte[] Capture();
}

public interface IDesktopDialog
{
    string Title { get; }
    string Text { get; }
    IReadOnlyList<string> Buttons { get; }
    void Click(string buttonText);
    byte[] Capture();
}

public sealed class DesktopException : Exception
{
    public DesktopException(string message, Exception? inner = null) : base(message, inner) { }
}

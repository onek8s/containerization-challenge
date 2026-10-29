using System.Runtime.InteropServices;
using PolicyChanges.Core.Flows;
using PolicyChanges.DeskRunner.Native;

namespace PolicyChanges.DeskRunner.Desktop;

/// <summary>Synthesised keyboard input. Goes to the foreground window of the input desktop.</summary>
internal static class Keyboard
{
    private const ushort VK_SHIFT = 0x10;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_MENU = 0x12;

    public static void Send(IEnumerable<KeyStroke> strokes, int delayMs = 15)
    {
        foreach (var k in strokes)
        {
            var inputs = new List<User32.INPUT>();
            var mods = Modifiers(k.Modifiers);
            foreach (var m in mods) inputs.Add(Key(m, 0));

            if (k.Character is { } c)
            {
                inputs.Add(Unicode(c, 0));
                inputs.Add(Unicode(c, User32.KEYEVENTF_KEYUP));
            }
            else
            {
                inputs.Add(Key(k.VirtualKey, 0));
                inputs.Add(Key(k.VirtualKey, User32.KEYEVENTF_KEYUP));
            }

            foreach (var m in mods.AsEnumerable().Reverse()) inputs.Add(Key(m, User32.KEYEVENTF_KEYUP));
            Dispatch(inputs.ToArray());
            if (delayMs > 0) Thread.Sleep(delayMs);
        }
    }

    public static void TapAlt() =>
        Dispatch(new[] { Key(VK_MENU, 0), Key(VK_MENU, User32.KEYEVENTF_KEYUP) });

    private static void Dispatch(User32.INPUT[] inputs)
    {
        var sent = User32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<User32.INPUT>());
        if (sent != inputs.Length)
            // SendInput fails silently when the desktop is locked or another desktop (UAC, Ctrl+Alt+Del) is active.
            throw new DesktopException($"Keyboard input was blocked (error {Marshal.GetLastWin32Error()}).");
    }

    private static List<ushort> Modifiers(KeyModifiers m)
    {
        var list = new List<ushort>();
        if (m.HasFlag(KeyModifiers.Control)) list.Add(VK_CONTROL);
        if (m.HasFlag(KeyModifiers.Shift)) list.Add(VK_SHIFT);
        if (m.HasFlag(KeyModifiers.Alt)) list.Add(VK_MENU);
        return list;
    }

    private static User32.INPUT Key(ushort vk, uint flags) => new()
    {
        Type = User32.INPUT_KEYBOARD,
        U = new User32.InputUnion { Keyboard = new User32.KEYBDINPUT { VirtualKey = vk, Flags = flags } },
    };

    private static User32.INPUT Unicode(char c, uint flags) => new()
    {
        Type = User32.INPUT_KEYBOARD,
        U = new User32.InputUnion { Keyboard = new User32.KEYBDINPUT { ScanCode = c, Flags = User32.KEYEVENTF_UNICODE | flags } },
    };
}

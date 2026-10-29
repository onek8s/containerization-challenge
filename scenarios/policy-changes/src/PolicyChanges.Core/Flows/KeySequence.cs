namespace PolicyChanges.Core.Flows;

/// <summary>
/// Keystroke notation used in flows: plain characters are typed as is, <c>{ENTER}</c>, <c>{TAB}</c>,
/// <c>{ESC}</c>, <c>{F1}</c>..<c>{F12}</c>, <c>{DEL}</c>, <c>{HOME}</c>, <c>{END}</c>, <c>{UP}</c>, <c>{DOWN}</c>;
/// modifiers <c>^</c> (Ctrl), <c>+</c> (Shift), <c>%</c> (Alt) apply to the next key; <c>{{}</c>, <c>{+}</c> etc. are literals.
/// </summary>
public static class KeySequence
{
    public static readonly IReadOnlyDictionary<string, ushort> VirtualKeys = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
    {
        ["ENTER"] = 0x0D, ["TAB"] = 0x09, ["ESC"] = 0x1B, ["BACKSPACE"] = 0x08, ["DEL"] = 0x2E, ["INS"] = 0x2D,
        ["HOME"] = 0x24, ["END"] = 0x23, ["PGUP"] = 0x21, ["PGDN"] = 0x22,
        ["LEFT"] = 0x25, ["UP"] = 0x26, ["RIGHT"] = 0x27, ["DOWN"] = 0x28, ["SPACE"] = 0x20,
        ["F1"] = 0x70, ["F2"] = 0x71, ["F3"] = 0x72, ["F4"] = 0x73, ["F5"] = 0x74, ["F6"] = 0x75,
        ["F7"] = 0x76, ["F8"] = 0x77, ["F9"] = 0x78, ["F10"] = 0x79, ["F11"] = 0x7A, ["F12"] = 0x7B,
    };

    public static IReadOnlyList<KeyStroke> Parse(string keys)
    {
        var result = new List<KeyStroke>();
        var modifiers = KeyModifiers.None;
        var i = 0;
        while (i < keys.Length)
        {
            var c = keys[i];
            switch (c)
            {
                case '^': modifiers |= KeyModifiers.Control; i++; continue;
                case '+': modifiers |= KeyModifiers.Shift; i++; continue;
                case '%': modifiers |= KeyModifiers.Alt; i++; continue;
                case '{':
                    var close = keys.IndexOf('}', i + 2);
                    if (close < 0) throw new FormatException($"Unclosed key name in '{keys}'.");
                    var name = keys[(i + 1)..close];
                    if (name.Length == 1)
                        result.Add(Char(name[0], modifiers));
                    else if (VirtualKeys.TryGetValue(name, out var vk))
                        result.Add(new KeyStroke(vk, null, modifiers));
                    else
                        throw new FormatException($"Unknown key {{{name}}}.");
                    i = close + 1;
                    break;
                default:
                    result.Add(Char(c, modifiers));
                    i++;
                    break;
            }
            modifiers = KeyModifiers.None;
        }
        if (modifiers != KeyModifiers.None) throw new FormatException($"Modifier without key in '{keys}'.");
        return result;
    }

    private static KeyStroke Char(char c, KeyModifiers modifiers)
    {
        // With modifiers the key is sent as a virtual key (Ctrl+C), otherwise as a Unicode character.
        if (modifiers == KeyModifiers.None) return new KeyStroke(0, c, modifiers);
        var upper = char.ToUpperInvariant(c);
        if (!char.IsAsciiLetterOrDigit(upper)) throw new FormatException($"Cannot combine modifiers with '{c}'.");
        return new KeyStroke(upper, null, modifiers);
    }

    /// <summary>Escapes text so that it is typed literally.</summary>
    public static string Escape(string text)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in text)
        {
            if (c is '^' or '+' or '%' or '{' or '}') sb.Append('{').Append(c).Append('}');
            else sb.Append(c);
        }
        return sb.ToString();
    }
}

[Flags]
public enum KeyModifiers
{
    None = 0,
    Control = 1,
    Shift = 2,
    Alt = 4,
}

/// <summary>Either a virtual key (<see cref="VirtualKey"/>) or a Unicode character.</summary>
public readonly record struct KeyStroke(ushort VirtualKey, char? Character, KeyModifiers Modifiers);

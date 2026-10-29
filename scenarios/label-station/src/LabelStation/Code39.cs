namespace LabelStation;

/// <summary>
/// Minimal Code 39 barcode encoder. Each character is 9 elements (5 bars, 4 spaces), 3 of them wide.
/// </summary>
public static class Code39
{
    private static readonly Dictionary<char, string> Patterns = new()
    {
        ['0'] = "nnnwwnwnn", ['1'] = "wnnwnnnnw", ['2'] = "nnwwnnnnw", ['3'] = "wnwwnnnnn",
        ['4'] = "nnnwwnnnw", ['5'] = "wnnwwnnnn", ['6'] = "nnwwwnnnn", ['7'] = "nnnwnnwnw",
        ['8'] = "wnnwnnwnn", ['9'] = "nnwwnnwnn", ['A'] = "wnnnnwnnw", ['B'] = "nnwnnwnnw",
        ['C'] = "wnwnnwnnn", ['D'] = "nnnnwwnnw", ['E'] = "wnnnwwnnn", ['F'] = "nnwnwwnnn",
        ['G'] = "nnnnnwwnw", ['H'] = "wnnnnwwnn", ['I'] = "nnwnnwwnn", ['J'] = "nnnnwwwnn",
        ['K'] = "wnnnnnnww", ['L'] = "nnwnnnnww", ['M'] = "wnwnnnnwn", ['N'] = "nnnnwnnww",
        ['O'] = "wnnnwnnwn", ['P'] = "nnwnwnnwn", ['Q'] = "nnnnnnwww", ['R'] = "wnnnnnwwn",
        ['S'] = "nnwnnnwwn", ['T'] = "nnnnwnwwn", ['U'] = "wwnnnnnnw", ['V'] = "nwwnnnnnw",
        ['W'] = "wwwnnnnnn", ['X'] = "nwnnwnnnw", ['Y'] = "wwnnwnnnn", ['Z'] = "nwwnwnnnn",
        ['-'] = "nwnnnnwnw", ['.'] = "wwnnnnwnn", [' '] = "nwwnnnwnn", ['*'] = "nwnnwnwnn",
        ['$'] = "nwnwnwnnn", ['/'] = "nwnwnnnwn", ['+'] = "nwnnnwnwn", ['%'] = "nnnwnwnwn",
    };

    public static void Draw(Graphics g, string text, RectangleF bounds)
    {
        var encoded = "*" + text.ToUpperInvariant() + "*";
        foreach (var c in encoded)
        {
            if (!Patterns.ContainsKey(c))
            {
                throw new ArgumentException($"Character '{c}' cannot be encoded in Code 39.", nameof(text));
            }
        }

        // Narrow = 1 unit, wide = 3 units, 1 unit gap between characters.
        const float wideRatio = 3f;
        var unitsPerChar = 6 + 3 * wideRatio + 1;
        var narrow = bounds.Width / (encoded.Length * unitsPerChar);

        var x = bounds.Left;
        foreach (var c in encoded)
        {
            var pattern = Patterns[c];
            for (var i = 0; i < pattern.Length; i++)
            {
                var width = pattern[i] == 'w' ? narrow * wideRatio : narrow;
                if (i % 2 == 0)
                {
                    g.FillRectangle(Brushes.Black, x, bounds.Top, width, bounds.Height);
                }
                x += width;
            }
            x += narrow;
        }
    }
}

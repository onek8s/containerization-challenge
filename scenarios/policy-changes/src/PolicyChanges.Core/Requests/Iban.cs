using System.Numerics;
using System.Text;

namespace PolicyChanges.Core.Requests;

public static class Iban
{
    private static readonly Dictionary<string, int> Lengths = new()
    {
        ["FI"] = 18, ["SE"] = 24, ["NO"] = 15, ["EE"] = 20, ["DK"] = 18, ["DE"] = 22,
    };

    public static string Normalize(string? value) =>
        new string((value ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    public static bool IsValid(string? value)
    {
        var iban = Normalize(value);
        if (iban.Length < 15 || !iban.All(char.IsAsciiLetterOrDigit)) return false;
        if (!Lengths.TryGetValue(iban[..2], out var length) || iban.Length != length) return false;

        var rearranged = iban[4..] + iban[..4];
        var digits = new StringBuilder();
        foreach (var c in rearranged)
            digits.Append(char.IsAsciiDigit(c) ? c.ToString() : (c - 'A' + 10).ToString());
        return BigInteger.Parse(digits.ToString()) % 97 == 1;
    }

    /// <summary>PolicyDesk shows IBANs in groups of four.</summary>
    public static string Format(string? value)
    {
        var iban = Normalize(value);
        return string.Join(' ', Enumerable.Range(0, (iban.Length + 3) / 4)
            .Select(i => iban.Substring(i * 4, Math.Min(4, iban.Length - i * 4))));
    }
}

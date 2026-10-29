using System.Numerics;
using System.Text;

namespace Lucerne.Lending.Domain;

public static class Iban
{
    public static string Normalize(string iban) => new string(iban.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    public static bool IsValid(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban)) return false;
        var s = Normalize(iban);
        if (s.Length < 15 || s.Length > 34 || !s.All(char.IsLetterOrDigit)) return false;
        if (s.StartsWith("FI") && s.Length != 18) return false;
        var rearranged = s[4..] + s[..4];
        var digits = new StringBuilder();
        foreach (var c in rearranged)
            digits.Append(char.IsLetter(c) ? (c - 'A' + 10).ToString() : c.ToString());
        return BigInteger.Parse(digits.ToString()) % 97 == 1;
    }

    public static string Format(string iban)
    {
        var s = Normalize(iban);
        return string.Join(' ', Enumerable.Range(0, (s.Length + 3) / 4).Select(i => s.Substring(i * 4, Math.Min(4, s.Length - i * 4))));
    }

    /// <summary>Lucerne's own accounts: FIkk 4790 ...</summary>
    public static bool IsOwnBank(string iban) => Normalize(iban).Length == 18 && Normalize(iban).Substring(4, 4) == "4790";
}

using System.Numerics;

namespace ExpenseClaims.Core.Payments;

public static class Iban
{
    public static string Normalize(string value) =>
        new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    /// <summary>ISO 13616 check: length, country code and mod-97 check digits.</summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var iban = Normalize(value);
        if (iban.Length is < 15 or > 34 || !char.IsLetter(iban[0]) || !char.IsLetter(iban[1]) || !char.IsDigit(iban[2]) || !char.IsDigit(iban[3]))
            return false;
        if (iban.StartsWith("FI", StringComparison.Ordinal) && iban.Length != 18)
            return false;

        var rearranged = iban[4..] + iban[..4];
        var digits = new System.Text.StringBuilder(rearranged.Length * 2);
        foreach (var c in rearranged)
        {
            if (char.IsDigit(c))
                digits.Append(c);
            else if (c is >= 'A' and <= 'Z')
                digits.Append(c - 'A' + 10);
            else
                return false;
        }
        return BigInteger.Parse(digits.ToString()) % 97 == 1;
    }

    public static string Format(string value)
    {
        var iban = Normalize(value);
        return string.Join(' ', Enumerable.Range(0, (iban.Length + 3) / 4).Select(i => iban.Substring(i * 4, Math.Min(4, iban.Length - i * 4))));
    }
}

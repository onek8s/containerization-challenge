using System.Text.RegularExpressions;

namespace PolicyChanges.Core.Requests;

/// <summary>PolicyDesk policy numbers: <c>HL-</c> followed by six digits and a Luhn check digit.</summary>
public static partial class PolicyNumber
{
    [GeneratedRegex(@"^HL-?(\d{7})$", RegexOptions.IgnoreCase)]
    private static partial Regex Pattern();

    public static string Normalize(string? value)
    {
        var m = Pattern().Match((value ?? "").Trim().Replace(" ", ""));
        return m.Success ? "HL-" + m.Groups[1].Value : (value ?? "").Trim().ToUpperInvariant();
    }

    public static bool IsValid(string? value)
    {
        var m = Pattern().Match(Normalize(value));
        return m.Success && Luhn.IsValid(m.Groups[1].Value);
    }

    public static string Create(int serial)
    {
        var body = serial.ToString("D6");
        return "HL-" + body + Luhn.CheckDigit(body);
    }
}

public static class Luhn
{
    public static bool IsValid(string digits) =>
        digits.Length > 1 && digits.All(char.IsAsciiDigit) && CheckDigit(digits[..^1]) == digits[^1] - '0';

    public static int CheckDigit(string digits)
    {
        var sum = 0;
        var doubleIt = true;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (doubleIt)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            sum += d;
            doubleIt = !doubleIt;
        }
        return (10 - sum % 10) % 10;
    }
}

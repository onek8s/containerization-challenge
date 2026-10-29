namespace Proseware.ControlTower.Domain.Shipments;

/// <summary>GS1 Serial Shipping Container Code.</summary>
public static class Sscc
{
    /// <summary>Proseware's GS1 company prefix.</summary>
    public const string DefaultCompanyPrefix = "6430012345";

    public static string Create(char extensionDigit, string companyPrefix, long serial)
    {
        if (!char.IsDigit(extensionDigit)) throw new ArgumentException("Extension digit must be 0-9.", nameof(extensionDigit));
        var serialLength = 16 - companyPrefix.Length;
        if (serialLength <= 0) throw new ArgumentException("Company prefix too long.", nameof(companyPrefix));
        var serialText = serial.ToString().PadLeft(serialLength, '0');
        if (serialText.Length > serialLength) throw new ArgumentOutOfRangeException(nameof(serial));
        var body = extensionDigit + companyPrefix + serialText;
        return body + CheckDigit(body);
    }

    public static bool IsValid(string? sscc)
    {
        if (sscc is null) return false;
        var s = Normalize(sscc);
        return s.Length == 18 && s.All(char.IsDigit) && CheckDigit(s[..17]) == s[17] - '0';
    }

    /// <summary>Strips the "(00)" application identifier and blanks that scanners or people add.</summary>
    public static string Normalize(string raw)
    {
        var s = raw.Replace(" ", "", StringComparison.Ordinal).Replace("(00)", "", StringComparison.Ordinal);
        if (s.Length == 20 && s.StartsWith("00", StringComparison.Ordinal)) s = s[2..];
        if (s.StartsWith("]C1", StringComparison.Ordinal)) s = Normalize(s[3..]);
        return s;
    }

    public static int CheckDigit(string digits17)
    {
        var sum = 0;
        for (var i = 0; i < digits17.Length; i++)
        {
            var d = digits17[i] - '0';
            sum += (i % 2 == 0) ? d * 3 : d;
        }
        return (10 - sum % 10) % 10;
    }
}

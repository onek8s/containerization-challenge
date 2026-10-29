using System.Globalization;
using System.Text.RegularExpressions;

namespace BatchRelease.Core.Certificates;

public static partial class CertificateNumbers
{
    /// <summary>Formats a certificate number, e.g. RC-TKU-2026-00042 or RJ-TKU-2026-00007.</summary>
    public static string Format(bool release, string siteCode, int year, int sequence)
    {
        if (string.IsNullOrWhiteSpace(siteCode) || siteCode.Length > 6)
            throw new ArgumentException("Site code must be 1-6 characters.", nameof(siteCode));
        if (sequence is < 1 or > 99999)
            throw new ArgumentOutOfRangeException(nameof(sequence));
        return string.Create(CultureInfo.InvariantCulture,
            $"{(release ? "RC" : "RJ")}-{siteCode.ToUpperInvariant()}-{year:0000}-{sequence:00000}");
    }

    public static bool TryParse(string? value, out bool release, out string siteCode, out int year, out int sequence)
    {
        release = false;
        siteCode = "";
        year = sequence = 0;
        if (value is null)
            return false;
        var m = Pattern().Match(value);
        if (!m.Success)
            return false;
        release = m.Groups[1].Value == "RC";
        siteCode = m.Groups[2].Value;
        year = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
        sequence = int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);
        return true;
    }

    [GeneratedRegex(@"^(RC|RJ)-([A-Z0-9]{1,6})-(\d{4})-(\d{5})$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}

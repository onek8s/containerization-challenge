using System.Globalization;

namespace ExpenseClaims.Core.Claims;

public static class ClaimNumber
{
    public static string Format(int year, int sequence) =>
        string.Create(CultureInfo.InvariantCulture, $"EC-{year}-{sequence:000000}");

    public static bool TryParse(string? value, out int year, out int sequence)
    {
        year = 0;
        sequence = 0;
        if (value is null || value.Length != 14 || !value.StartsWith("EC-", StringComparison.Ordinal) || value[7] != '-')
            return false;
        return int.TryParse(value.AsSpan(3, 4), NumberStyles.None, CultureInfo.InvariantCulture, out year)
            && int.TryParse(value.AsSpan(8, 6), NumberStyles.None, CultureInfo.InvariantCulture, out sequence);
    }
}

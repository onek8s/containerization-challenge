using CityPermits.Domain.Model;

namespace CityPermits.Domain.Workflow;

public static class ReferenceNumbers
{
    public static string Prefix(PermitKind kind) => kind switch
    {
        PermitKind.BuildingPermit => "BP",
        PermitKind.BusinessLicence => "BL",
        PermitKind.ParkingPermit => "PP",
        _ => "XX"
    };

    /// <summary>BP/2024/00417 style reference with a mod-11 check letter (legacy records system format).</summary>
    public static string Format(PermitKind kind, int year, int sequence)
    {
        var body = $"{Prefix(kind)}/{year}/{sequence:00000}";
        return body + CheckLetter(year, sequence);
    }

    public static char CheckLetter(int year, int sequence)
    {
        var digits = $"{year % 100:00}{sequence:00000}";
        var sum = 0;
        for (var i = 0; i < digits.Length; i++) sum += (digits[i] - '0') * (8 - i % 7);
        return "ABCDEFGHJKL"[sum % 11];
    }

    public static bool IsValid(string reference)
    {
        var parts = reference.Split('/');
        if (parts.Length != 3 || parts[2].Length != 6) return false;
        if (!int.TryParse(parts[1], out var year) || !int.TryParse(parts[2][..5], out var seq)) return false;
        return CheckLetter(year, seq) == parts[2][5];
    }
}

using System.Text.RegularExpressions;

namespace Lucerne.Lending.Domain;

/// <summary>Finnish personal identity code (henkilötunnus), DDMMYYCZZZQ.</summary>
public static class PersonalIdentityCode
{
    private const string CheckChars = "0123456789ABCDEFHJKLMNPRSTUVWXY";
    private static readonly Regex Pattern = new(@"^(\d{6})([-+ABCDEFYXWVU])(\d{3})([0-9A-Y])$", RegexOptions.Compiled);

    public static bool IsValid(string? code) => TryParse(code, out _, out _);

    public static bool TryParse(string? code, out DateOnly birthDate, out bool female)
    {
        birthDate = default;
        female = false;
        if (string.IsNullOrWhiteSpace(code)) return false;
        var m = Pattern.Match(code.Trim().ToUpperInvariant());
        if (!m.Success) return false;
        var digits = m.Groups[1].Value;
        var century = m.Groups[2].Value[0] switch
        {
            '+' => 1800,
            '-' or 'Y' or 'X' or 'W' or 'V' or 'U' => 1900,
            _ => 2000
        };
        var individual = m.Groups[3].Value;
        var number = long.Parse(digits + individual);
        if (CheckChars[(int)(number % 31)] != m.Groups[4].Value[0]) return false;
        var day = int.Parse(digits[..2]);
        var month = int.Parse(digits.Substring(2, 2));
        var year = century + int.Parse(digits.Substring(4, 2));
        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)) return false;
        birthDate = new DateOnly(year, month, day);
        female = int.Parse(individual) % 2 == 0;
        return true;
    }

    public static int AgeOn(string code, DateOnly date)
    {
        if (!TryParse(code, out var birth, out _)) throw new FormatException("Invalid personal identity code.");
        var age = date.Year - birth.Year;
        if (date < birth.AddYears(age)) age--;
        return age;
    }

    public static string Mask(string code) => code.Length >= 7 ? code[..7] + "****" : "****";
}

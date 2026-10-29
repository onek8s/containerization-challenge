using System.Globalization;

namespace PolicyChanges.Core.Requests;

public sealed record Beneficiary(string Name, DateOnly BirthDate, int Share)
{
    public static IReadOnlyList<Beneficiary> ParseList(string? text, out List<string> errors)
    {
        errors = new List<string>();
        var result = new List<Beneficiary>();
        var lines = (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < lines.Length; i++)
        {
            var parts = lines[i].Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length != 3)
            {
                errors.Add($"Beneficiary line {i + 1}: expected name|yyyy-MM-dd|share.");
                continue;
            }
            if (parts[0].Length is < 2 or > 60)
                errors.Add($"Beneficiary line {i + 1}: name must be 2-60 characters.");
            if (!DateOnly.TryParseExact(parts[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var birth))
                errors.Add($"Beneficiary line {i + 1}: invalid date of birth.");
            if (!int.TryParse(parts[2].TrimEnd('%'), NumberStyles.None, CultureInfo.InvariantCulture, out var share) || share is < 1 or > 100)
                errors.Add($"Beneficiary line {i + 1}: share must be 1-100.");
            result.Add(new Beneficiary(parts[0], birth, share));
        }
        return result;
    }

    public string ToLine() => $"{Name}|{BirthDate:yyyy-MM-dd}|{Share}";
}

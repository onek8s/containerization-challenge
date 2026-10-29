namespace Humongous.Quoting.Domain;

/// <summary>Cover sections of the Business Combined product.</summary>
public enum SectionCode
{
    MaterialDamage,
    BusinessInterruption,
    EmployersLiability,
    PublicLiability
}

public static class SectionCodes
{
    // Two letter codes used by RateCore, the bordereau and the documents.
    private static readonly Dictionary<SectionCode, string> Codes = new()
    {
        [SectionCode.MaterialDamage] = "MD",
        [SectionCode.BusinessInterruption] = "BI",
        [SectionCode.EmployersLiability] = "EL",
        [SectionCode.PublicLiability] = "PL"
    };

    private static readonly Dictionary<SectionCode, string> Names = new()
    {
        [SectionCode.MaterialDamage] = "Material Damage",
        [SectionCode.BusinessInterruption] = "Business Interruption",
        [SectionCode.EmployersLiability] = "Employers' Liability",
        [SectionCode.PublicLiability] = "Public and Products Liability"
    };

    public static string ToCode(this SectionCode section) => Codes[section];

    public static string DisplayName(this SectionCode section) => Names[section];

    public static SectionCode Parse(string code)
    {
        foreach (var pair in Codes)
        {
            if (string.Equals(pair.Value, code?.Trim(), StringComparison.OrdinalIgnoreCase))
                return pair.Key;
        }
        throw new ArgumentException($"Unknown section code '{code}'.", nameof(code));
    }

    public static bool TryParse(string? code, out SectionCode section)
    {
        foreach (var pair in Codes)
        {
            if (string.Equals(pair.Value, code?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                section = pair.Key;
                return true;
            }
        }
        section = default;
        return false;
    }
}

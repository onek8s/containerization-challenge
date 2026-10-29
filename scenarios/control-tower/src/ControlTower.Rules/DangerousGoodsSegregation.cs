namespace Proseware.ControlTower.Rules;

public enum SegregationResult
{
    Allowed,
    /// <summary>Allowed with conditions (e.g. separate stowage, compatibility groups) - shown as a warning.</summary>
    Conditional,
    Prohibited
}

/// <summary>
/// Mixed loading of dangerous goods in one vehicle, simplified from ADR 7.5.2.1 (by label) and 7.5.4
/// (foodstuffs). Compatibility groups of class 1 are not evaluated: every pair with class 1 except 1.4S
/// is treated as prohibited.
/// </summary>
public static class DangerousGoodsSegregation
{
    private static readonly string[] AllClasses = { "1", "2.1", "2.2", "2.3", "3", "4.1", "4.2", "4.3", "5.1", "5.2", "6.1", "6.2", "7", "8", "9" };

    // X = prohibited, C = conditional, . = allowed. Rows/columns in AllClasses order.
    private static readonly string[] Table =
    {
        //         1  2.1 2.2 2.3 3  4.1 4.2 4.3 5.1 5.2 6.1 6.2 7  8  9
        /* 1   */ "C  X   X   X   X  X   X   X   X   X   X   X   X  X  C",
        /* 2.1 */ "X  .   .   .   .  .   .   .   .   .   .   .   .  .  .",
        /* 2.2 */ "X  .   .   .   .  .   .   .   .   .   .   .   .  .  .",
        /* 2.3 */ "X  .   .   .   .  .   .   .   .   .   .   .   .  .  .",
        /* 3   */ "X  .   .   .   .  .   .   .   .   .   .   .   .  .  .",
        /* 4.1 */ "X  .   .   .   .  .   .   .   .   C   .   .   .  .  .",
        /* 4.2 */ "X  .   .   .   .  .   .   .   .   .   .   .   .  .  .",
        /* 4.3 */ "X  .   .   .   .  .   .   .   .   .   .   .   .  .  .",
        /* 5.1 */ "X  .   .   .   .  .   .   .   .   .   .   .   .  .  .",
        /* 5.2 */ "X  .   .   .   .  C   .   .   .   .   .   .   .  .  .",
        /* 6.1 */ "X  .   .   .   .  .   .   .   .   .   .   .   .  .  .",
        /* 6.2 */ "X  .   .   .   .  .   .   .   .   .   .   .   .  .  .",
        /* 7   */ "X  .   .   .   .  .   .   .   .   .   .   .   .  .  .",
        /* 8   */ "X  .   .   .   .  .   .   .   .   .   .   .   .  .  .",
        /* 9   */ "C  .   .   .   .  .   .   .   .   .   .   .   .  .  .",
    };

    private static readonly char[][] Matrix = Table.Select(r => r.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(c => c[0]).ToArray()).ToArray();

    public static SegregationResult Check(string classA, string classB)
    {
        var a = Index(classA);
        var b = Index(classB);
        if (a < 0 || b < 0) return SegregationResult.Allowed;
        if (IsOnePointFourS(classA) || IsOnePointFourS(classB)) return SegregationResult.Allowed;
        return Matrix[a][b] switch
        {
            'X' => SegregationResult.Prohibited,
            'C' => SegregationResult.Conditional,
            _ => SegregationResult.Allowed
        };
    }

    /// <summary>ADR 7.5.4: classes 6.1 and 6.2 (and 9 UN 2212/2315/2590/3151/3152/3245) are kept apart from food.</summary>
    public static bool MustBeKeptFromFood(string hazardClass, string unNumber) =>
        hazardClass is "6.1" or "6.2" || (hazardClass == "9" && unNumber is "2212" or "2315" or "2590" or "3151" or "3152" or "3245");

    public static string Normalize(string hazardClass)
    {
        var c = hazardClass.Trim();
        if (c.StartsWith("1.", StringComparison.Ordinal) && !IsOnePointFourS(c)) return "1";
        return c;
    }

    private static int Index(string hazardClass) => Array.IndexOf(AllClasses, Normalize(hazardClass));

    private static bool IsOnePointFourS(string c) => c.Replace(" ", "", StringComparison.Ordinal).Equals("1.4S", StringComparison.OrdinalIgnoreCase);
}

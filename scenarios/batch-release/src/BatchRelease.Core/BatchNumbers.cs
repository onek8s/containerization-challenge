using System.Text.RegularExpressions;

namespace BatchRelease.Core;

public static partial class BatchNumbers
{
    /// <summary>Batch numbers are site letter + 2-digit year + 5 digits + optional sub-lot letter, e.g. T2600418 or T2600418B.</summary>
    public static bool IsValid(string? batchNumber) => batchNumber is not null && Pattern().IsMatch(batchNumber);

    [GeneratedRegex(@"^[A-Z]\d{2}\d{5}[A-Z]?$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}

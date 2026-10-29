using System.Text.RegularExpressions;

namespace FurnaceRecords.Core;

public static partial class LoadNumbers
{
    /// <summary>Load numbers are furnace code, year and a running number: F03-26-00412.</summary>
    public static string Format(string furnaceCode, int year, int sequence) =>
        $"{furnaceCode}-{year % 100:00}-{sequence:00000}";

    public static bool IsValid(string loadNumber) => Pattern().IsMatch(loadNumber);

    [GeneratedRegex(@"^[A-Z][A-Z0-9]{1,5}-\d{2}-\d{5}$")]
    private static partial Regex Pattern();
}

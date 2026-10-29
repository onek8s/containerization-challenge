using System.Globalization;
using System.Text.RegularExpressions;

namespace Litware.DocControl.Transmittals;

public static partial class TransmittalNumber
{
    [GeneratedRegex(@"^TR-P\d{4}-\d{5}$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public static string Format(string project, int sequence) =>
        string.Create(CultureInfo.InvariantCulture, $"TR-{project}-{sequence:00000}");

    public static string CounterName(string project) => "TR-" + project;

    public static bool IsValid(string? text) => text is not null && Pattern().IsMatch(text);
}

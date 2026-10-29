using System.Text.RegularExpressions;

namespace Humongous.Quoting.Domain;

public static partial class Postcode
{
    [GeneratedRegex(@"^(GIR 0AA|[A-PR-UWYZ]([0-9]{1,2}|[A-HK-Y][0-9]{1,2}|[0-9][A-HJKS-UW]|[A-HK-Y][0-9][ABEHMNPRV-Y]) [0-9][ABD-HJLNP-UW-Z]{2})$")]
    private static partial Regex Pattern();

    public static string Normalise(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var compact = value.Replace(" ", "").Trim().ToUpperInvariant();
        return compact.Length > 3 ? $"{compact[..^3]} {compact[^3..]}" : compact;
    }

    public static bool IsValid(string? value) => Pattern().IsMatch(Normalise(value));

    /// <summary>Outward code (district), e.g. "LS1" from "LS1 4AP". Rating zones are keyed by district.</summary>
    public static string District(string value)
    {
        var normalised = Normalise(value);
        var space = normalised.IndexOf(' ');
        return space > 0 ? normalised[..space] : normalised;
    }

    /// <summary>Postcode area, e.g. "LS" from "LS1 4AP".</summary>
    public static string Area(string value) =>
        new(District(value).TakeWhile(char.IsLetter).ToArray());
}

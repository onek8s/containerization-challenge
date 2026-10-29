using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Litware.DocControl.Documents;

/// <summary>
/// Project document number: <c>{Project}-{Originator}-{Discipline}-{Type}-{Sequence}</c>,
/// for example <c>P1042-LIT-ME-DRG-0012</c>. Numbering follows the Litware engineering
/// procedure EP-07 (document numbering and revision control).
/// </summary>
public sealed partial record DocumentNumber(string Project, string Originator, string Discipline, string DocumentType, int Sequence)
{
    public static readonly IReadOnlyDictionary<string, string> Disciplines = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["GN"] = "General",
        ["AR"] = "Architectural",
        ["CI"] = "Civil",
        ["ST"] = "Structural",
        ["ME"] = "Mechanical",
        ["PI"] = "Piping",
        ["PR"] = "Process",
        ["EL"] = "Electrical",
        ["IN"] = "Instrumentation",
        ["HV"] = "HVAC",
    };

    public static readonly IReadOnlyDictionary<string, string> DocumentTypes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["DRG"] = "Drawing",
        ["SPC"] = "Specification",
        ["CAL"] = "Calculation",
        ["DSH"] = "Datasheet",
        ["RPT"] = "Report",
        ["LST"] = "List / schedule",
        ["PID"] = "P&ID",
    };

    [GeneratedRegex(@"^(?<project>P\d{4})-(?<orig>[A-Z]{3})-(?<disc>[A-Z]{2})-(?<type>[A-Z]{3})-(?<seq>\d{4})$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public static bool TryParse(string? text, [NotNullWhen(true)] out DocumentNumber? number)
    {
        number = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var m = Pattern().Match(text.Trim().ToUpperInvariant());
        if (!m.Success) return false;

        var disc = m.Groups["disc"].Value;
        var type = m.Groups["type"].Value;
        if (!Disciplines.ContainsKey(disc) || !DocumentTypes.ContainsKey(type)) return false;

        var seq = int.Parse(m.Groups["seq"].Value, System.Globalization.CultureInfo.InvariantCulture);
        if (seq == 0) return false;

        number = new DocumentNumber(m.Groups["project"].Value, m.Groups["orig"].Value, disc, type, seq);
        return true;
    }

    public static DocumentNumber Parse(string text) =>
        TryParse(text, out var n) ? n : throw new FormatException($"'{text}' is not a valid document number.");

    public override string ToString() => $"{Project}-{Originator}-{Discipline}-{DocumentType}-{Sequence:0000}";
}

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Litware.DocControl.Documents;

/// <summary>
/// Revision codes per EP-07: preliminary issues P01..P99, then construction issues C01..C99.
/// Once a document has a C revision it can no longer go back to P.
/// </summary>
public readonly record struct RevisionCode(char Stage, int Number) : IComparable<RevisionCode>
{
    public bool IsPreliminary => Stage == 'P';
    public bool IsConstruction => Stage == 'C';

    public static RevisionCode FirstPreliminary => new('P', 1);
    public static RevisionCode FirstConstruction => new('C', 1);

    public static bool TryParse(string? text, [NotNullWhen(true)] out RevisionCode code)
    {
        code = default;
        if (text is null) return false;
        text = text.Trim().ToUpperInvariant();
        if (text.Length != 3 || (text[0] != 'P' && text[0] != 'C')) return false;
        if (!int.TryParse(text.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 1) return false;
        code = new RevisionCode(text[0], n);
        return true;
    }

    public static RevisionCode Parse(string text) =>
        TryParse(text, out var c) ? c : throw new FormatException($"'{text}' is not a valid revision code.");

    public RevisionCode NextInStage() =>
        Number >= 99 ? throw new InvalidOperationException($"No revisions left after {this}.") : new RevisionCode(Stage, Number + 1);

    public int CompareTo(RevisionCode other)
    {
        if (Stage != other.Stage) return Stage == 'P' ? -1 : 1;
        return Number.CompareTo(other.Number);
    }

    public static bool operator <(RevisionCode a, RevisionCode b) => a.CompareTo(b) < 0;
    public static bool operator >(RevisionCode a, RevisionCode b) => a.CompareTo(b) > 0;

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Stage}{Number:00}");
}

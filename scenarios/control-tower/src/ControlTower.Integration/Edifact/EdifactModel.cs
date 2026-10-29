using System.Text;

namespace Proseware.ControlTower.Integration.Edifact;

/// <summary>Service string advice (UNA). Defaults are the UNOA/UNOC standard characters.</summary>
public sealed record EdifactSeparators(char Component, char Element, char Decimal, char Release, char Segment)
{
    public static readonly EdifactSeparators Default = new(':', '+', '.', '?', '\'');

    public string ToUna() => $"UNA{Component}{Element}{Decimal}{Release} {Segment}";
}

/// <summary>One data element; simple elements have a single component.</summary>
public sealed class EdifactElement
{
    public EdifactElement(params string[] components) => Components = components.ToList();

    public List<string> Components { get; }

    public string Value => Components.Count > 0 ? Components[0] : "";

    public string this[int index] => index < Components.Count ? Components[index] : "";

    public bool IsEmpty => Components.All(string.IsNullOrEmpty);

    public override string ToString() => string.Join(":", Components);
}

public sealed class EdifactSegment
{
    public EdifactSegment(string tag, IEnumerable<EdifactElement>? elements = null)
    {
        Tag = tag;
        Elements = elements?.ToList() ?? new List<EdifactElement>();
    }

    public string Tag { get; }

    public List<EdifactElement> Elements { get; }

    /// <summary>Element by position (0-based, after the tag); empty element when missing.</summary>
    public EdifactElement this[int index] => index < Elements.Count ? Elements[index] : new EdifactElement();

    public string Get(int element, int component = 0) => this[element][component];

    public EdifactSegment Add(params string[] components)
    {
        Elements.Add(new EdifactElement(components));
        return this;
    }

    /// <summary>Segment with a single composite element, e.g. DTM+137:202610290712:203.</summary>
    public static EdifactSegment Composite(string tag, params string[] components) =>
        new(tag, new[] { new EdifactElement(components) });

    /// <summary>
    /// Segment from element values; a string[] value is a composite element. A segment with only one
    /// composite element must be built with <see cref="Composite"/> (a lone array would be taken as the element list).
    /// </summary>
    public static EdifactSegment Create(string tag, params object?[] elements)
    {
        var seg = new EdifactSegment(tag);
        foreach (var e in elements)
        {
            seg.Elements.Add(e switch
            {
                null => new EdifactElement(""),
                string[] parts => new EdifactElement(parts),
                _ => new EdifactElement(Convert.ToString(e, System.Globalization.CultureInfo.InvariantCulture) ?? "")
            });
        }
        return seg;
    }

    public override string ToString() => Tag + "+" + string.Join("+", Elements);
}

/// <summary>A message between UNH and UNT.</summary>
public sealed class EdifactMessage
{
    public string ReferenceNumber { get; set; } = "";
    public string Type { get; set; } = "";
    public string Version { get; set; } = "D";
    public string Release { get; set; } = "96A";
    public string Agency { get; set; } = "UN";
    public string? AssociationCode { get; set; }

    /// <summary>Segments between UNH and UNT (exclusive).</summary>
    public List<EdifactSegment> Segments { get; } = new();

    public IEnumerable<EdifactSegment> All(string tag) => Segments.Where(s => s.Tag == tag);

    public EdifactSegment? First(string tag) => Segments.FirstOrDefault(s => s.Tag == tag);

    /// <summary>
    /// Splits the message into segment groups that start with <paramref name="triggerTag"/>; a group
    /// runs until the next trigger or until a segment in <paramref name="stopTags"/>.
    /// </summary>
    public IEnumerable<List<EdifactSegment>> Groups(string triggerTag, params string[] stopTags)
    {
        List<EdifactSegment>? current = null;
        foreach (var seg in Segments)
        {
            if (seg.Tag == triggerTag)
            {
                if (current is not null) yield return current;
                current = new List<EdifactSegment> { seg };
            }
            else if (current is not null)
            {
                if (stopTags.Contains(seg.Tag))
                {
                    yield return current;
                    current = null;
                }
                else
                {
                    current.Add(seg);
                }
            }
        }
        if (current is not null) yield return current;
    }
}

public sealed class EdifactInterchange
{
    public EdifactSeparators Separators { get; set; } = EdifactSeparators.Default;
    public string SyntaxIdentifier { get; set; } = "UNOC";
    public string SyntaxVersion { get; set; } = "3";
    public string SenderId { get; set; } = "";
    public string? SenderQualifier { get; set; }
    public string RecipientId { get; set; } = "";
    public string? RecipientQualifier { get; set; }
    public DateTime Prepared { get; set; }
    public string ControlReference { get; set; } = "";
    public string? ApplicationReference { get; set; }
    public bool TestIndicator { get; set; }
    public List<EdifactMessage> Messages { get; } = new();

    public static Encoding EncodingFor(string syntaxIdentifier) => syntaxIdentifier switch
    {
        "UNOA" or "UNOB" => Encoding.ASCII,
        "UNOC" => Encoding.Latin1,
        "UNOW" or "UNOY" => Encoding.UTF8,
        _ => Encoding.Latin1
    };
}

public sealed class EdifactException : Exception
{
    public EdifactException(string message, int? segmentNumber = null) : base(segmentNumber is null ? message : $"{message} (segment {segmentNumber})")
    {
        SegmentNumber = segmentNumber;
    }

    public int? SegmentNumber { get; }
}

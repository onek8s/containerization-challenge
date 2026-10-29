using System.Text;

namespace Proseware.ControlTower.Integration.Edifact;

public static class EdifactWriter
{
    /// <summary>Writes the interchange. UNB/UNH/UNT/UNZ are generated from the model.</summary>
    public static string Write(EdifactInterchange interchange, bool writeUna = true, bool lineBreaks = true)
    {
        var sep = interchange.Separators;
        var sb = new StringBuilder();
        if (writeUna) sb.Append(sep.ToUna());
        var nl = lineBreaks ? "\r\n" : "";
        if (writeUna) sb.Append(nl);

        var unb = EdifactSegment.Create("UNB",
            new[] { interchange.SyntaxIdentifier, interchange.SyntaxVersion },
            Composite(interchange.SenderId, interchange.SenderQualifier),
            Composite(interchange.RecipientId, interchange.RecipientQualifier),
            new[] { interchange.Prepared.ToString(interchange.SyntaxVersion == "4" ? "yyyyMMdd" : "yyMMdd"), interchange.Prepared.ToString("HHmm") },
            interchange.ControlReference);
        if (interchange.ApplicationReference is not null || interchange.TestIndicator)
        {
            unb.Add("");
            unb.Add(interchange.ApplicationReference ?? "");
            if (interchange.TestIndicator)
            {
                unb.Add("");
                unb.Add("");
                unb.Add("");
                unb.Add("1");
            }
        }
        AppendSegment(sb, unb, sep, nl);

        foreach (var message in interchange.Messages)
        {
            var type = new List<string> { message.Type, message.Version, message.Release, message.Agency };
            if (message.AssociationCode is not null) type.Add(message.AssociationCode);
            AppendSegment(sb, EdifactSegment.Create("UNH", message.ReferenceNumber, type.ToArray()), sep, nl);
            foreach (var seg in message.Segments) AppendSegment(sb, seg, sep, nl);
            AppendSegment(sb, EdifactSegment.Create("UNT", message.Segments.Count + 2, message.ReferenceNumber), sep, nl);
        }
        AppendSegment(sb, EdifactSegment.Create("UNZ", interchange.Messages.Count, interchange.ControlReference), sep, nl);
        return sb.ToString();
    }

    public static string Escape(string value, EdifactSeparators sep)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c == sep.Release || c == sep.Component || c == sep.Element || c == sep.Segment) sb.Append(sep.Release);
            sb.Append(c is '\r' or '\n' ? ' ' : c);
        }
        return sb.ToString();
    }

    private static void AppendSegment(StringBuilder sb, EdifactSegment seg, EdifactSeparators sep, string nl)
    {
        sb.Append(seg.Tag);
        // trailing empty elements and components are omitted
        var elements = seg.Elements.ToList();
        while (elements.Count > 0 && elements[^1].IsEmpty) elements.RemoveAt(elements.Count - 1);
        foreach (var e in elements)
        {
            sb.Append(sep.Element);
            var comps = e.Components.ToList();
            while (comps.Count > 1 && string.IsNullOrEmpty(comps[^1])) comps.RemoveAt(comps.Count - 1);
            sb.Append(string.Join(sep.Component, comps.Select(c => Escape(c, sep))));
        }
        sb.Append(sep.Segment).Append(nl);
    }

    private static string[] Composite(string id, string? qualifier) =>
        qualifier is null ? new[] { id } : new[] { id, qualifier };
}

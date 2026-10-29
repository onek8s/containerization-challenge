using System.Globalization;
using System.Text;

namespace Proseware.ControlTower.Integration.Edifact;

/// <summary>Tokenizes and parses EDIFACT interchanges (syntax versions 3 and 4).</summary>
public static class EdifactReader
{
    public static EdifactInterchange Parse(string text)
    {
        var separators = EdifactSeparators.Default;
        var body = text.TrimStart('﻿');
        if (body.StartsWith("UNA", StringComparison.Ordinal))
        {
            if (body.Length < 9) throw new EdifactException("UNA segment is truncated.");
            separators = new EdifactSeparators(body[3], body[4], body[5], body[6], body[8]);
            body = body[9..];
        }

        var segments = Tokenize(body, separators).ToList();
        if (segments.Count == 0 || segments[0].Tag != "UNB") throw new EdifactException("Interchange does not start with UNB.");

        var unb = segments[0];
        var interchange = new EdifactInterchange
        {
            Separators = separators,
            SyntaxIdentifier = unb.Get(0, 0),
            SyntaxVersion = unb.Get(0, 1),
            SenderId = unb.Get(1, 0),
            SenderQualifier = NullIfEmpty(unb.Get(1, 1)),
            RecipientId = unb.Get(2, 0),
            RecipientQualifier = NullIfEmpty(unb.Get(2, 1)),
            Prepared = ParseUnbDate(unb.Get(3, 0), unb.Get(3, 1)),
            ControlReference = unb.Get(4),
            ApplicationReference = NullIfEmpty(unb.Get(6)),
            TestIndicator = unb.Get(10) == "1"
        };

        EdifactMessage? current = null;
        var segmentCount = 0;
        var unzSeen = false;
        for (var i = 1; i < segments.Count; i++)
        {
            var seg = segments[i];
            switch (seg.Tag)
            {
                case "UNG":
                case "UNE":
                    break;
                case "UNH":
                    if (current is not null) throw new EdifactException("UNH without preceding UNT.", i + 1);
                    current = new EdifactMessage
                    {
                        ReferenceNumber = seg.Get(0),
                        Type = seg.Get(1, 0),
                        Version = seg.Get(1, 1),
                        Release = seg.Get(1, 2),
                        Agency = seg.Get(1, 3),
                        AssociationCode = NullIfEmpty(seg.Get(1, 4))
                    };
                    segmentCount = 1;
                    break;
                case "UNT":
                    if (current is null) throw new EdifactException("UNT without UNH.", i + 1);
                    segmentCount++;
                    if (int.TryParse(seg.Get(0), out var declared) && declared != segmentCount)
                        throw new EdifactException($"UNT segment count {declared} does not match {segmentCount} in message {current.ReferenceNumber}.", i + 1);
                    if (seg.Get(1) != current.ReferenceNumber)
                        throw new EdifactException($"UNT reference {seg.Get(1)} does not match UNH {current.ReferenceNumber}.", i + 1);
                    interchange.Messages.Add(current);
                    current = null;
                    break;
                case "UNZ":
                    if (seg.Get(1) != interchange.ControlReference)
                        throw new EdifactException($"UNZ reference {seg.Get(1)} does not match UNB {interchange.ControlReference}.", i + 1);
                    if (int.TryParse(seg.Get(0), out var count) && count != interchange.Messages.Count)
                        throw new EdifactException($"UNZ message count {count} does not match {interchange.Messages.Count}.", i + 1);
                    unzSeen = true;
                    break;
                default:
                    if (current is null) throw new EdifactException($"Segment {seg.Tag} outside of a message.", i + 1);
                    current.Segments.Add(seg);
                    segmentCount++;
                    break;
            }
        }
        if (current is not null) throw new EdifactException($"Message {current.ReferenceNumber} has no UNT.");
        if (!unzSeen) throw new EdifactException("Interchange has no UNZ.");
        return interchange;
    }

    public static IEnumerable<EdifactSegment> Tokenize(string body, EdifactSeparators sep)
    {
        var elements = new List<EdifactElement>();
        var components = new List<string>();
        var token = new StringBuilder();
        string? tag = null;

        void EndComponent()
        {
            components.Add(token.ToString());
            token.Clear();
        }

        void EndElement()
        {
            EndComponent();
            if (tag is null)
            {
                tag = components[0].Trim();
            }
            else
            {
                elements.Add(new EdifactElement(components.ToArray()));
            }
            components.Clear();
        }

        for (var i = 0; i < body.Length; i++)
        {
            var c = body[i];
            if (c == sep.Release)
            {
                if (++i < body.Length) token.Append(body[i]);
                continue;
            }
            if (c == sep.Component)
            {
                EndComponent();
            }
            else if (c == sep.Element)
            {
                EndElement();
            }
            else if (c == sep.Segment)
            {
                EndElement();
                if (!string.IsNullOrEmpty(tag)) yield return new EdifactSegment(tag, elements);
                elements = new List<EdifactElement>();
                tag = null;
            }
            else if ((c == '\r' || c == '\n') && token.Length == 0 && components.Count == 0 && tag is null)
            {
                // line breaks between segments
            }
            else
            {
                token.Append(c);
            }
        }
        if (tag is not null || token.ToString().Trim().Length > 0)
            throw new EdifactException("Last segment is not terminated.");
    }

    /// <summary>Parses DTM date/time values by format qualifier (102, 203, 204, 303).</summary>
    public static DateTimeOffset? ParseDtm(string value, string format)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var inv = CultureInfo.InvariantCulture;
        switch (format)
        {
            case "102":
                return DateTime.TryParseExact(value, "yyyyMMdd", inv, DateTimeStyles.None, out var d)
                    ? new DateTimeOffset(d, TimeSpan.Zero) : null;
            case "203":
                return DateTime.TryParseExact(value, "yyyyMMddHHmm", inv, DateTimeStyles.None, out var dt)
                    ? new DateTimeOffset(dt, TimeSpan.Zero) : null;
            case "204":
                return DateTime.TryParseExact(value, "yyyyMMddHHmmss", inv, DateTimeStyles.None, out var ds)
                    ? new DateTimeOffset(ds, TimeSpan.Zero) : null;
            case "303":
                // CCYYMMDDHHMMZZZ, ZZZ = time zone like "+02" or "UTC"
                if (value.Length < 12) return null;
                if (!DateTime.TryParseExact(value[..12], "yyyyMMddHHmm", inv, DateTimeStyles.None, out var dz)) return null;
                var zone = value[12..];
                var offset = TimeSpan.Zero;
                if (zone.Length >= 3 && (zone[0] == '+' || zone[0] == '-') && int.TryParse(zone[1..3], out var h))
                    offset = TimeSpan.FromHours(zone[0] == '-' ? -h : h);
                return new DateTimeOffset(dz, offset);
            default:
                return null;
        }
    }

    private static DateTime ParseUnbDate(string date, string time)
    {
        var format = date.Length == 8 ? "yyyyMMdd" : "yyMMdd";
        DateTime.TryParseExact(date + time, format + "HHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result);
        return result;
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;
}

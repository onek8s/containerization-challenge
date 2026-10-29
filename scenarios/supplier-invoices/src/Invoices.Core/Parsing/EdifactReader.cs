using System.Collections.Generic;
using System.Text;

namespace Wingtip.Invoices.Core.Parsing
{
    /// <summary>
    /// Splits an EDIFACT interchange into segments. Honours the UNA service string advice
    /// and the release character.
    /// </summary>
    public static class EdifactReader
    {
        public static IList<EdifactSegment> Read(string content)
        {
            char component = ':', element = '+', release = '?', terminator = '\'';
            var text = content.TrimStart('﻿', ' ', '\r', '\n');

            if (text.StartsWith("UNA"))
            {
                if (text.Length < 9) throw new InvoiceParseException("Truncated UNA segment.");
                component = text[3];
                element = text[4];
                release = text[6];
                terminator = text[8];
                text = text.Substring(9);
            }

            var segments = new List<EdifactSegment>();
            var elements = new List<string[]>();
            var components = new List<string>();
            var current = new StringBuilder();
            string tag = null;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];

                if (c == release && i + 1 < text.Length)
                {
                    current.Append(text[++i]);
                    continue;
                }

                if (c == '\r' || c == '\n')
                {
                    continue;
                }

                if (c == component)
                {
                    components.Add(current.ToString());
                    current.Clear();
                }
                else if (c == element || c == terminator)
                {
                    components.Add(current.ToString());
                    current.Clear();

                    if (tag == null)
                    {
                        tag = components[0].Trim();
                    }
                    else
                    {
                        elements.Add(components.ToArray());
                    }
                    components.Clear();

                    if (c == terminator)
                    {
                        if (tag.Length > 0)
                        {
                            segments.Add(new EdifactSegment(tag, elements));
                        }
                        tag = null;
                        elements = new List<string[]>();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (tag != null || current.ToString().Trim().Length > 0)
            {
                throw new InvoiceParseException("The interchange does not end with a segment terminator.");
            }

            return segments;
        }
    }
}

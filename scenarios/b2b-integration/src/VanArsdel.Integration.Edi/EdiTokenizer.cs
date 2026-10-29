using System;
using System.Collections.Generic;
using System.Text;

namespace VanArsdel.Integration.Edi
{
    /// <summary>Splits raw interchange text into segments, honouring the EDIFACT release character.</summary>
    public static class EdiTokenizer
    {
        public static EdiStandard Detect(string text)
        {
            var start = SkipWhitespace(text, 0);
            if (string.CompareOrdinal(text, start, "ISA", 0, 3) == 0)
            {
                return EdiStandard.X12;
            }

            if (string.CompareOrdinal(text, start, "UNA", 0, 3) == 0 || string.CompareOrdinal(text, start, "UNB", 0, 3) == 0)
            {
                return EdiStandard.Edifact;
            }

            throw new EdiException("The data does not start with ISA, UNA or UNB.");
        }

        public static Delimiters ReadDelimiters(string text, EdiStandard standard)
        {
            var start = SkipWhitespace(text, 0);
            if (standard == EdiStandard.X12)
            {
                // ISA is fixed length: the element separator is the 4th character, ISA16 (component separator) is at 104
                // and the segment terminator follows it.
                if (text.Length < start + 106)
                {
                    throw new EdiException("ISA segment is shorter than 106 characters.");
                }

                var element = text[start + 3];
                var component = text[start + 104];
                var segment = text[start + 105];
                var repetitionField = text.Substring(start + 82, 1);
                var version = text.Substring(start + 84, 5);
                char? repetition = null;
                if (string.CompareOrdinal(version, "00402") >= 0 && repetitionField != "U")
                {
                    repetition = repetitionField[0];
                }

                return new Delimiters(element, component, segment, repetition, null, '.');
            }

            if (string.CompareOrdinal(text, start, "UNA", 0, 3) == 0)
            {
                if (text.Length < start + 9)
                {
                    throw new EdiException("UNA service string advice is incomplete.");
                }

                var release = text[start + 6];
                return new Delimiters(text[start + 4], text[start + 3], text[start + 8], null, release == ' ' ? (char?)null : release, text[start + 5]);
            }

            return Delimiters.EdifactDefault;
        }

        public static IList<Segment> Tokenize(string text, Delimiters d)
        {
            var segments = new List<Segment>();
            var i = SkipWhitespace(text, 0);
            if (string.CompareOrdinal(text, i, "UNA", 0, 3) == 0)
            {
                i = SkipWhitespace(text, i + 9);
            }

            var elements = new List<string[]>();
            var components = new List<string>();
            var current = new StringBuilder();
            string tag = null;

            while (i < text.Length)
            {
                var ch = text[i];
                if (d.Release.HasValue && ch == d.Release.Value && i + 1 < text.Length)
                {
                    current.Append(text[i + 1]);
                    i += 2;
                    continue;
                }

                if (ch == d.Segment)
                {
                    FlushElement(ref tag, elements, components, current);
                    if (tag != null)
                    {
                        segments.Add(new Segment(tag, elements) { Position = segments.Count + 1 });
                    }

                    tag = null;
                    elements = new List<string[]>();
                    i = SkipWhitespace(text, i + 1);
                    continue;
                }

                if (ch == d.Element)
                {
                    FlushElement(ref tag, elements, components, current);
                }
                else if (ch == d.Component && tag != null && !(tag == "ISA"))
                {
                    components.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(ch);
                }

                i++;
            }

            FlushElement(ref tag, elements, components, current);
            if (tag != null && tag.Trim().Length > 0)
            {
                segments.Add(new Segment(tag.Trim(), elements) { Position = segments.Count + 1 });
            }

            return segments;
        }

        private static void FlushElement(ref string tag, List<string[]> elements, List<string> components, StringBuilder current)
        {
            if (tag == null)
            {
                var t = current.ToString().Trim();
                tag = t.Length == 0 ? null : t;
                current.Clear();
                components.Clear();
                return;
            }

            components.Add(current.ToString());
            elements.Add(components.ToArray());
            components.Clear();
            current.Clear();
        }

        private static int SkipWhitespace(string text, int i)
        {
            while (i < text.Length && (text[i] == '\r' || text[i] == '\n' || text[i] == ' ' || text[i] == '\t' || text[i] == '\0'))
            {
                i++;
            }

            return i;
        }
    }

    [Serializable]
    public class EdiException : Exception
    {
        public EdiException(string message) : base(message)
        {
        }

        public EdiException(string message, Exception inner) : base(message, inner)
        {
        }

        protected EdiException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }
}

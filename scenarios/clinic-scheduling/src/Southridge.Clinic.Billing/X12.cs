using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Southridge.Clinic.Billing
{
    /// <summary>ASC X12 segment reader. Separators are taken from the ISA segment (element at 3, component at 104, segment at 105).</summary>
    public class X12Reader
    {
        private readonly List<string[]> _segments = new List<string[]>();

        public X12Reader(string content)
        {
            if (content == null || content.Length < 106 || !content.StartsWith("ISA", StringComparison.Ordinal))
            {
                throw new FormatException("Not an X12 interchange (missing ISA).");
            }

            ElementSeparator = content[3];
            ComponentSeparator = content[104];
            SegmentTerminator = content[105];
            foreach (string raw in content.Split(SegmentTerminator))
            {
                string segment = raw.Trim('\r', '\n', ' ');
                if (segment.Length > 0)
                {
                    _segments.Add(segment.Split(ElementSeparator));
                }
            }
        }

        public char ElementSeparator { get; private set; }

        public char ComponentSeparator { get; private set; }

        public char SegmentTerminator { get; private set; }

        public IList<string[]> Segments
        {
            get { return _segments; }
        }

        public static string Element(string[] segment, int index)
        {
            return index < segment.Length ? segment[index] : string.Empty;
        }

        public static decimal Amount(string[] segment, int index)
        {
            string value = Element(segment, index);
            return string.IsNullOrEmpty(value) ? 0m : decimal.Parse(value, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        }

        public static DateTime Date(string value)
        {
            return DateTime.ParseExact(value, value.Length == 6 ? "yyMMdd" : "yyyyMMdd", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Writes X12 segments (composite elements are passed already joined with the component separator) with '*' elements, ':' components and '~' terminators, counting segments for SE.</summary>
    public class X12Writer
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private int _transactionSegments;

        public bool LineBreaks { get; set; }

        public int TransactionSegmentCount
        {
            get { return _transactionSegments; }
        }

        public void Segment(string id, params string[] elements)
        {
            int last = elements.Length - 1;
            while (last >= 0 && string.IsNullOrEmpty(elements[last]))
            {
                last--;
            }

            _sb.Append(id);
            for (int i = 0; i <= last; i++)
            {
                _sb.Append('*').Append(Clean(elements[i]));
            }

            _sb.Append('~');
            if (LineBreaks)
            {
                _sb.Append("\r\n");
            }

            if (id == "ST")
            {
                _transactionSegments = 1;
            }
            else
            {
                _transactionSegments++;
            }
        }

        /// <summary>The ISA segment is fixed length: elements are padded, not trimmed.</summary>
        public void Raw(string segment)
        {
            _sb.Append(segment).Append('~');
            if (LineBreaks)
            {
                _sb.Append("\r\n");
            }
        }

        public override string ToString()
        {
            return _sb.ToString();
        }

        public static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(value.Length);
            foreach (char c in value.ToUpperInvariant())
            {
                if (c == '*' || c == '~' || c == '^' || c < ' ' || c > '~')
                {
                    continue;
                }

                sb.Append(c);
            }

            return sb.ToString().Trim();
        }
    }
}

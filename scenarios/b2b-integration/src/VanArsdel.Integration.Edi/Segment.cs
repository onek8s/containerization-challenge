using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VanArsdel.Integration.Edi
{
    public enum EdiStandard
    {
        X12 = 1,
        Edifact = 2
    }

    /// <summary>Separators of an interchange. X12 takes them from the ISA segment, EDIFACT from UNA or the defaults.</summary>
    public sealed class Delimiters
    {
        public Delimiters(char element, char component, char segment, char? repetition, char? release, char decimalMark)
        {
            Element = element;
            Component = component;
            Segment = segment;
            Repetition = repetition;
            Release = release;
            DecimalMark = decimalMark;
        }

        public char Element { get; private set; }
        public char Component { get; private set; }
        public char Segment { get; private set; }
        public char? Repetition { get; private set; }
        public char? Release { get; private set; }
        public char DecimalMark { get; private set; }

        /// <summary>The separators we use on outbound X12 (004010): * &gt; ~ with U as ISA11.</summary>
        public static Delimiters X12Default
        {
            get { return new Delimiters('*', '>', '~', null, null, '.'); }
        }

        /// <summary>UN/EDIFACT defaults (UNA:+.? ').</summary>
        public static Delimiters EdifactDefault
        {
            get { return new Delimiters('+', ':', '\'', null, '?', '.'); }
        }

        public string ToUna()
        {
            return "UNA" + Component + Element + DecimalMark + (Release ?? ' ') + ' ' + Segment;
        }
    }

    /// <summary>One segment: tag plus data elements, each element split into its components.</summary>
    public sealed class Segment
    {
        private readonly List<string[]> _elements;

        public Segment(string tag, IEnumerable<string[]> elements)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                throw new ArgumentException("A segment needs a tag.", "tag");
            }

            Tag = tag.Trim();
            _elements = (elements ?? Enumerable.Empty<string[]>()).ToList();
        }

        public static Segment Of(string tag, params string[] simpleElements)
        {
            return new Segment(tag, simpleElements.Select(e => e == null ? new string[0] : new[] { e }));
        }

        public string Tag { get; private set; }

        /// <summary>Position in the transaction set, ST/UNH = 1.</summary>
        public int Position { get; set; }

        public IList<string[]> Elements
        {
            get { return _elements; }
        }

        public int ElementCount
        {
            get { return _elements.Count; }
        }

        /// <summary>Element value by 1-based position (first component), empty when missing.</summary>
        public string this[int position]
        {
            get { return Component(position, 1); }
        }

        public string Component(int position, int component)
        {
            if (position < 1 || position > _elements.Count)
            {
                return string.Empty;
            }

            var e = _elements[position - 1];
            return component >= 1 && component <= e.Length ? e[component - 1] ?? string.Empty : string.Empty;
        }

        public string ToEdi(Delimiters d)
        {
            var sb = new StringBuilder(Tag);
            var last = _elements.Count;
            while (last > 0 && IsEmpty(_elements[last - 1]))
            {
                last--;
            }

            for (var i = 0; i < last; i++)
            {
                sb.Append(d.Element);
                var comps = _elements[i];
                var lastComp = comps.Length;
                while (lastComp > 0 && string.IsNullOrEmpty(comps[lastComp - 1]))
                {
                    lastComp--;
                }

                for (var c = 0; c < lastComp; c++)
                {
                    if (c > 0)
                    {
                        sb.Append(d.Component);
                    }

                    sb.Append(Escape(comps[c] ?? string.Empty, d));
                }
            }

            sb.Append(d.Segment);
            return sb.ToString();
        }

        public override string ToString()
        {
            return ToEdi(Delimiters.X12Default);
        }

        private static bool IsEmpty(string[] element)
        {
            return element == null || element.All(string.IsNullOrEmpty);
        }

        private static string Escape(string value, Delimiters d)
        {
            if (!d.Release.HasValue)
            {
                return value;
            }

            var r = d.Release.Value;
            var sb = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                if (ch == r || ch == d.Element || ch == d.Component || ch == d.Segment)
                {
                    sb.Append(r);
                }

                sb.Append(ch);
            }

            return sb.ToString();
        }
    }
}

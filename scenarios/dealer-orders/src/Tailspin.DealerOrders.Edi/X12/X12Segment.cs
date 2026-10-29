using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Tailspin.DealerOrders.Edi.X12
{
    public sealed class X12Delimiters
    {
        public static readonly X12Delimiters Default = new X12Delimiters('*', '~', ':', 'U');

        public X12Delimiters(char element, char segment, char component, char repetition)
        {
            Element = element;
            Segment = segment;
            Component = component;
            Repetition = repetition;
        }

        public char Element { get; }
        public char Segment { get; }
        public char Component { get; }

        /// <summary>ISA11: repetition separator from 4020 on, the standards identifier 'U' in 4010.</summary>
        public char Repetition { get; }
    }

    /// <summary>One X12 segment: the segment id and its elements (index 1 = first element, as in the guides).</summary>
    public sealed class X12Segment
    {
        private readonly List<string> _elements;

        public X12Segment(string id, IEnumerable<string> elements)
        {
            Id = id;
            _elements = elements.ToList();
        }

        public X12Segment(string id, params object[] elements)
            : this(id, elements.Select(Format))
        {
        }

        public string Id { get; }
        public int Count => _elements.Count;
        public IReadOnlyList<string> Elements => _elements;

        public string this[int position] => position >= 1 && position <= _elements.Count ? _elements[position - 1] : "";

        public string[] Components(int position, char separator) => this[position].Split(separator);

        public decimal Decimal(int position) =>
            string.IsNullOrEmpty(this[position]) ? 0m : decimal.Parse(this[position], NumberStyles.Number, CultureInfo.InvariantCulture);

        public DateTime? Date(int position)
        {
            var value = this[position];
            if (string.IsNullOrEmpty(value)) return null;
            var format = value.Length == 6 ? "yyMMdd" : "yyyyMMdd";
            return DateTime.ParseExact(value, format, CultureInfo.InvariantCulture);
        }

        /// <summary>Returns the value following a qualifier in qualifier/value pairs, e.g. PO1 06/07, 08/09 ...</summary>
        public string ValueAfterQualifier(string qualifier, int firstPairPosition)
        {
            for (var i = firstPairPosition; i < Count; i += 2)
                if (string.Equals(this[i], qualifier, StringComparison.Ordinal)) return this[i + 1];
            return null;
        }

        public string ToString(X12Delimiters delimiters)
        {
            var trimmed = _elements.ToList();
            while (trimmed.Count > 0 && string.IsNullOrEmpty(trimmed[trimmed.Count - 1])) trimmed.RemoveAt(trimmed.Count - 1);
            return Id + (trimmed.Count == 0 ? "" : delimiters.Element + string.Join(delimiters.Element.ToString(), trimmed)) + delimiters.Segment;
        }

        public override string ToString() => ToString(X12Delimiters.Default);

        private static string Format(object value)
        {
            switch (value)
            {
                case null:
                    return "";
                case decimal d:
                    return d.ToString("0.####", CultureInfo.InvariantCulture);
                case DateTime dt:
                    return dt.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                case IFormattable f:
                    return f.ToString(null, CultureInfo.InvariantCulture);
                default:
                    return value.ToString();
            }
        }
    }
}

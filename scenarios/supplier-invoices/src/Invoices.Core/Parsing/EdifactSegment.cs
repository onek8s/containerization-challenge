using System.Collections.Generic;

namespace Wingtip.Invoices.Core.Parsing
{
    public class EdifactSegment
    {
        public EdifactSegment(string tag, IList<string[]> elements)
        {
            Tag = tag;
            Elements = elements;
        }

        public string Tag { get; private set; }

        /// <summary>Data elements after the tag; each element is split into its components.</summary>
        public IList<string[]> Elements { get; private set; }

        public string Get(int element, int component = 0)
        {
            if (element >= Elements.Count) return null;
            var parts = Elements[element];
            if (component >= parts.Length) return null;
            return parts[component].Length == 0 ? null : parts[component];
        }

        public override string ToString()
        {
            return Tag;
        }
    }
}

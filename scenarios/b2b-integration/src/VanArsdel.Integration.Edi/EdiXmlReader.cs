using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using VanArsdel.Integration.Edi.Definitions;

namespace VanArsdel.Integration.Edi
{
    /// <summary>
    /// Reads EDI XML (the output of a map to X12_00401_810, EFACT_D96A_DESADV, ...) back into segments, the way the
    /// EDI assembler serializes it. ST/SE and UNH/UNT counts and control numbers are set by the interchange builder.
    /// </summary>
    public static class EdiXmlReader
    {
        public static TransactionDefinition DefinitionFor(XmlDocument document)
        {
            var root = document.DocumentElement;
            if (root == null)
            {
                throw new EdiException("The document is empty.");
            }

            var def = TransactionCatalog.FindByRoot(root.LocalName);
            if (def == null)
            {
                throw new EdiException("No EDI definition for root " + root.LocalName + ".");
            }

            return def;
        }

        public static IList<Segment> Read(XmlDocument document)
        {
            var def = DefinitionFor(document);
            var segments = new List<Segment>();
            ReadChildren(document.DocumentElement, def, segments);
            return segments;
        }

        private static void ReadChildren(XmlElement parent, TransactionDefinition def, List<Segment> segments)
        {
            foreach (var child in parent.ChildNodes.OfType<XmlElement>())
            {
                if (child.LocalName.Contains("Loop"))
                {
                    ReadChildren(child, def, segments);
                    continue;
                }

                var sd = def.FindSegment(child.LocalName);
                if (sd == null)
                {
                    throw new EdiException("Segment " + child.LocalName + " is not part of " + def.RootName + ".");
                }

                segments.Add(ToSegment(sd, child));
            }
        }

        private static Segment ToSegment(SegmentDef def, XmlElement element)
        {
            var values = new string[def.Elements.Length][];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = new string[0];
            }

            foreach (var field in element.ChildNodes.OfType<XmlElement>())
            {
                var index = def.IndexOf(field.LocalName);
                if (index < 0)
                {
                    throw new EdiException("Element " + field.LocalName + " is not part of segment " + def.Tag + ".");
                }

                var ed = def.Elements[index];
                if (ed.IsComposite)
                {
                    var comps = new string[ed.Components.Length];
                    foreach (var c in field.ChildNodes.OfType<XmlElement>())
                    {
                        var ci = Array.FindIndex(ed.Components, x => x.Name == c.LocalName);
                        if (ci < 0)
                        {
                            throw new EdiException("Component " + c.LocalName + " is not part of " + ed.Name + ".");
                        }

                        comps[ci] = c.InnerText;
                    }

                    values[index] = comps;
                }
                else
                {
                    values[index] = new[] { field.InnerText };
                }
            }

            return new Segment(def.Tag, values);
        }
    }
}

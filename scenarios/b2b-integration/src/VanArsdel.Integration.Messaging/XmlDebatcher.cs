using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace VanArsdel.Integration.Messaging
{
    public class DebatchedPart
    {
        public int Index { get; set; }
        public int Count { get; set; }
        public byte[] Body { get; set; }
        public string MessageType { get; set; }

        /// <summary>Attributes of the envelope root (BatchId, PartnerId on OrderBatch), copied to every part's context.</summary>
        public IDictionary<string, string> EnvelopeAttributes { get; set; }
    }

    /// <summary>
    /// Splits an XML envelope into its child documents without loading the whole file, the way the XML Debatcher
    /// disassembler does for gateway batches (OrderBatch/Order) and IDoc files with several IDOC elements.
    /// Namespace declarations in scope on the envelope are carried into each part.
    /// </summary>
    public static class XmlDebatcher
    {
        /// <param name="bodyElement">Local name of the repeating element, e.g. "Order" or "IDOC".</param>
        /// <param name="wrapInRoot">When set, each part is wrapped in a copy of the envelope root (used for IDocs:
        /// one ORDERS05 document per IDOC).</param>
        public static IList<DebatchedPart> Split(Stream input, string bodyElement, bool wrapInRoot = false)
        {
            var parts = new List<DebatchedPart>();
            var settings = new XmlReaderSettings { IgnoreWhitespace = true, IgnoreComments = true, DtdProcessing = DtdProcessing.Prohibit, CloseInput = false };
            using (var reader = XmlReader.Create(input, settings))
            {
                reader.MoveToContent();
                var rootName = reader.LocalName;
                var rootNs = reader.NamespaceURI;
                var rootPrefix = reader.Prefix;
                var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
                var nsDecls = new Dictionary<string, string>(StringComparer.Ordinal);
                if (reader.MoveToFirstAttribute())
                {
                    do
                    {
                        if (reader.Prefix == "xmlns" || reader.Name == "xmlns")
                        {
                            nsDecls[reader.Prefix == "xmlns" ? reader.LocalName : string.Empty] = reader.Value;
                        }
                        else
                        {
                            attributes[reader.LocalName] = reader.Value;
                        }
                    }
                    while (reader.MoveToNextAttribute());
                    reader.MoveToElement();
                }

                if (reader.IsEmptyElement)
                {
                    return parts;
                }

                reader.Read();
                while (!reader.EOF)
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.LocalName == bodyElement && reader.Depth == 1)
                    {
                        var partNs = reader.NamespaceURI;
                        var xml = reader.ReadOuterXml();
                        parts.Add(new DebatchedPart
                        {
                            Index = parts.Count + 1,
                            Body = wrapInRoot ? Wrap(xml, rootPrefix, rootName, rootNs, nsDecls, attributes) : Normalize(xml),
                            MessageType = wrapInRoot ? Contracts.MessageTypes.Of(rootNs, rootName) : Contracts.MessageTypes.Of(partNs, bodyElement),
                            EnvelopeAttributes = attributes
                        });
                        continue;
                    }

                    if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == 0)
                    {
                        break;
                    }

                    if (reader.NodeType == XmlNodeType.Element && reader.Depth == 1)
                    {
                        reader.Skip();
                        continue;
                    }

                    reader.Read();
                }
            }

            foreach (var p in parts)
            {
                p.Count = parts.Count;
            }

            return parts;
        }

        private static byte[] Normalize(string xml)
        {
            var doc = new XmlDocument { PreserveWhitespace = false };
            doc.LoadXml(xml);
            return ToBytes(doc);
        }

        private static byte[] Wrap(string inner, string prefix, string rootName, string rootNs, IDictionary<string, string> nsDecls, IDictionary<string, string> attributes)
        {
            var doc = new XmlDocument();
            var root = doc.CreateElement(prefix, rootName, rootNs);
            foreach (var ns in nsDecls)
            {
                if (ns.Key != prefix)
                {
                    root.SetAttribute(ns.Key.Length == 0 ? "xmlns" : "xmlns:" + ns.Key, ns.Value);
                }
            }

            foreach (var a in attributes)
            {
                root.SetAttribute(a.Key, a.Value);
            }

            doc.AppendChild(root);
            var fragment = new XmlDocument();
            fragment.LoadXml(inner);
            root.AppendChild(doc.ImportNode(fragment.DocumentElement, true));
            return ToBytes(doc);
        }

        public static byte[] ToBytes(XmlDocument doc)
        {
            using (var ms = new MemoryStream())
            {
                using (var w = XmlWriter.Create(ms, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false, OmitXmlDeclaration = true }))
                {
                    doc.Save(w);
                }

                return ms.ToArray();
            }
        }
    }
}

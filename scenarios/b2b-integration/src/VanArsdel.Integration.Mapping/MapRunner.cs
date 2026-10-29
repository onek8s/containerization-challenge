using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Xsl;

namespace VanArsdel.Integration.Mapping
{
    /// <summary>
    /// Executes a map's XSLT with its extension objects (what the BizTalk transform engine does for a map with a
    /// custom XSLT). New extension object instances are created for every execution, like BizTalk does.
    /// </summary>
    public class MapRunner
    {
        private readonly XslCompiledTransform _transform;
        private readonly IList<ExtensionObjectDefinition> _extensions;

        public MapRunner(string name, string xsltPath, string extensionXmlPath)
        {
            Name = name;
            _transform = new XslCompiledTransform();
            using (var reader = XmlReader.Create(xsltPath, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
            {
                _transform.Load(reader, new XsltSettings(false, false), new XmlUrlResolver());
            }

            _extensions = string.IsNullOrEmpty(extensionXmlPath) || !File.Exists(extensionXmlPath)
                ? new List<ExtensionObjectDefinition>()
                : ExtensionObjects.Load(extensionXmlPath);
        }

        public string Name { get; private set; }

        public XmlDocument Execute(XmlDocument input, IDictionary<string, object> parameters = null)
        {
            var args = new XsltArgumentList();
            foreach (var e in _extensions)
            {
                args.AddExtensionObject(e.Namespace, ExtensionObjects.CreateInstance(e));
            }

            if (parameters != null)
            {
                foreach (var p in parameters)
                {
                    args.AddParam(p.Key, string.Empty, p.Value);
                }
            }

            var output = new XmlDocument();
            using (var ms = new MemoryStream())
            {
                using (var writer = XmlWriter.Create(ms, new XmlWriterSettings { Encoding = new UTF8Encoding(false), OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Auto }))
                {
                    _transform.Transform(new XmlNodeReader(input), args, writer);
                }

                ms.Position = 0;
                try
                {
                    output.Load(ms);
                }
                catch (XmlException ex)
                {
                    throw new MapException("Map " + Name + " produced no well-formed document: " + ex.Message, ex);
                }
            }

            return output;
        }

        public byte[] Execute(byte[] input)
        {
            var doc = new XmlDocument();
            doc.Load(new MemoryStream(input));
            var result = Execute(doc);
            using (var ms = new MemoryStream())
            {
                using (var writer = XmlWriter.Create(ms, new XmlWriterSettings { Encoding = new UTF8Encoding(false), OmitXmlDeclaration = true }))
                {
                    result.Save(writer);
                }

                return ms.ToArray();
            }
        }
    }

    [Serializable]
    public class MapException : Exception
    {
        public MapException(string message, Exception inner) : base(message, inner)
        {
        }

        protected MapException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }
}

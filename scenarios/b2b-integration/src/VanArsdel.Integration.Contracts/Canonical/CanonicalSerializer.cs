using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace VanArsdel.Integration.Contracts.Canonical
{
    /// <summary>XmlSerializer helpers for the canonical documents (serializers are cached per type).</summary>
    public static class CanonicalSerializer
    {
        private static readonly ConcurrentDictionary<Type, XmlSerializer> Cache = new ConcurrentDictionary<Type, XmlSerializer>();

        public static T Deserialize<T>(Stream stream)
        {
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings { IgnoreWhitespace = true, CloseInput = false }))
            {
                return (T)Get(typeof(T)).Deserialize(reader);
            }
        }

        public static T Deserialize<T>(string xml)
        {
            using (var reader = new StringReader(xml))
            {
                return (T)Get(typeof(T)).Deserialize(reader);
            }
        }

        public static T Deserialize<T>(XmlDocument document)
        {
            using (var reader = new XmlNodeReader(document))
            {
                return (T)Get(typeof(T)).Deserialize(reader);
            }
        }

        public static string Serialize<T>(T value)
        {
            var ns = new XmlSerializerNamespaces();
            ns.Add("ns0", MessageTypes.CanonicalNamespace);
            var builder = new StringBuilder();
            using (var writer = XmlWriter.Create(builder, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true }))
            {
                Get(typeof(T)).Serialize(writer, value, ns);
            }

            return builder.ToString();
        }

        public static void Serialize<T>(T value, Stream target)
        {
            var ns = new XmlSerializerNamespaces();
            ns.Add("ns0", MessageTypes.CanonicalNamespace);
            using (var writer = XmlWriter.Create(target, new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false), CloseOutput = false }))
            {
                Get(typeof(T)).Serialize(writer, value, ns);
            }
        }

        private static XmlSerializer Get(Type type)
        {
            return Cache.GetOrAdd(type, t => new XmlSerializer(t));
        }
    }
}

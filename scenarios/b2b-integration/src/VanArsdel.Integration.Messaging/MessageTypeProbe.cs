using System.IO;
using System.Text;
using System.Xml;

namespace VanArsdel.Integration.Messaging
{
    public enum PayloadFormat
    {
        Unknown = 0,
        Xml = 1,
        X12 = 2,
        Edifact = 3,
        FlatFile = 4
    }

    public class ProbeResult
    {
        public PayloadFormat Format { get; set; }

        /// <summary>namespace#root for XML payloads.</summary>
        public string MessageType { get; set; }
    }

    /// <summary>Looks at the start of a body to decide which disassembler gets it (IProbeMessage logic).</summary>
    public static class MessageTypeProbe
    {
        public static ProbeResult Probe(byte[] body)
        {
            var head = Encoding.UTF8.GetString(body, 0, System.Math.Min(body.Length, 512)).TrimStart('﻿', ' ', '\r', '\n', '\t');
            if (head.StartsWith("ISA", System.StringComparison.Ordinal))
            {
                return new ProbeResult { Format = PayloadFormat.X12 };
            }

            if (head.StartsWith("UNA", System.StringComparison.Ordinal) || head.StartsWith("UNB", System.StringComparison.Ordinal))
            {
                return new ProbeResult { Format = PayloadFormat.Edifact };
            }

            if (head.StartsWith("<", System.StringComparison.Ordinal))
            {
                try
                {
                    using (var reader = XmlReader.Create(new MemoryStream(body), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
                    {
                        reader.MoveToContent();
                        return new ProbeResult { Format = PayloadFormat.Xml, MessageType = Contracts.MessageTypes.Of(reader.NamespaceURI, reader.LocalName) };
                    }
                }
                catch (XmlException)
                {
                    return new ProbeResult { Format = PayloadFormat.Unknown };
                }
            }

            return new ProbeResult { Format = head.Length > 0 ? PayloadFormat.FlatFile : PayloadFormat.Unknown };
        }
    }
}

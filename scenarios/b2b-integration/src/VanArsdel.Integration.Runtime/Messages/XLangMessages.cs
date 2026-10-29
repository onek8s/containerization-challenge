using System;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.XLANGs.BaseTypes;
using VanArsdel.Integration.Contracts.Canonical;

namespace VanArsdel.Integration.Runtime.Messages
{
    /// <summary>
    /// Helpers for expression shapes. The message is passed as XLANGMessage (orchestration message variables
    /// convert implicitly); the part is always the first one, our messages are single-part.
    /// </summary>
    [Serializable]
    public static class XLangMessages
    {
        public static XmlDocument ToXmlDocument(XLANGMessage message)
        {
            try
            {
                return (XmlDocument)message[0].RetrieveAs(typeof(XmlDocument));
            }
            finally
            {
                message.Dispose();
            }
        }

        public static string ToText(XLANGMessage message)
        {
            try
            {
                using (var stream = (Stream)message[0].RetrieveAs(typeof(Stream)))
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    return reader.ReadToEnd();
                }
            }
            finally
            {
                message.Dispose();
            }
        }

        public static Order ToOrder(XLANGMessage message)
        {
            return CanonicalSerializer.Deserialize<Order>(ToXmlDocument(message));
        }

        public static Invoice ToInvoice(XLANGMessage message)
        {
            return CanonicalSerializer.Deserialize<Invoice>(ToXmlDocument(message));
        }

        public static FunctionalAck ToFunctionalAck(XLANGMessage message)
        {
            return CanonicalSerializer.Deserialize<FunctionalAck>(ToXmlDocument(message));
        }

        /// <summary>Message construction: OrderMsg = XLangMessages.FromOrder(order); in a Construct shape.</summary>
        public static XmlDocument FromCanonical<T>(T value)
        {
            var doc = new XmlDocument();
            doc.LoadXml(CanonicalSerializer.Serialize(value));
            return doc;
        }

        public static string PartName(XLANGMessage message)
        {
            try
            {
                return message[0].Name;
            }
            finally
            {
                message.Dispose();
            }
        }
    }
}

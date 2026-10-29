using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;

namespace VanArsdel.Integration.Messaging
{
    public class ValidationIssue
    {
        public XmlSeverityType Severity { get; set; }
        public string Message { get; set; }
        public int Line { get; set; }
        public int Position { get; set; }

        public override string ToString()
        {
            return Severity + " (" + Line + "," + Position + "): " + Message;
        }
    }

    /// <summary>
    /// XSD validation of XML bodies against the deployed schemas (the XML Validator stage). BizTalk annotations
    /// (b:schemaInfo, b:properties) are ignored by XmlSchemaSet. Loads every .xsd under the schema folder once.
    /// </summary>
    public class SchemaValidator
    {
        private readonly XmlSchemaSet _schemas;

        public SchemaValidator(XmlSchemaSet schemas)
        {
            _schemas = schemas;
        }

        public static SchemaValidator FromFolder(string folder)
        {
            var set = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
            foreach (var file in Directory.GetFiles(folder, "*.xsd", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            {
                if (file.EndsWith("Types.xsd", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(file).StartsWith("PropertySchema", StringComparison.OrdinalIgnoreCase))
                {
                    // included by the root schemas / not a document schema
                    continue;
                }

                using (var reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
                {
                    var schema = XmlSchema.Read(reader, null);
                    schema.SourceUri = new Uri(Path.GetFullPath(file)).AbsoluteUri;
                    set.Add(schema);
                }
            }

            set.Compile();
            return new SchemaValidator(set);
        }

        public bool HasSchemaFor(string messageType)
        {
            var hash = messageType.LastIndexOf('#');
            var ns = hash < 0 ? string.Empty : messageType.Substring(0, hash);
            var root = hash < 0 ? messageType : messageType.Substring(hash + 1);
            return _schemas.GlobalElements.Contains(new XmlQualifiedName(root, ns));
        }

        public IList<ValidationIssue> Validate(byte[] body)
        {
            var issues = new List<ValidationIssue>();
            var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema, Schemas = _schemas, DtdProcessing = DtdProcessing.Prohibit };
            settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
            settings.ValidationEventHandler += (s, e) => issues.Add(new ValidationIssue
            {
                Severity = e.Severity,
                Message = e.Message,
                Line = e.Exception == null ? 0 : e.Exception.LineNumber,
                Position = e.Exception == null ? 0 : e.Exception.LinePosition
            });

            try
            {
                using (var reader = XmlReader.Create(new MemoryStream(body), settings))
                {
                    while (reader.Read())
                    {
                    }
                }
            }
            catch (XmlException ex)
            {
                issues.Add(new ValidationIssue { Severity = XmlSeverityType.Error, Message = ex.Message, Line = ex.LineNumber, Position = ex.LinePosition });
            }

            return issues;
        }

        public void EnsureValid(byte[] body, string messageType)
        {
            var errors = Validate(body).Where(i => i.Severity == XmlSeverityType.Error).ToList();
            if (errors.Count > 0)
            {
                throw new MessageValidationException(messageType, errors);
            }
        }
    }

    [Serializable]
    public class MessageValidationException : Exception
    {
        public MessageValidationException(string messageType, IList<ValidationIssue> issues)
            : base("The " + messageType + " document failed validation: " + string.Join("; ", issues.Take(5).Select(i => i.ToString())))
        {
            MessageType = messageType;
            Issues = issues;
        }

        protected MessageValidationException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }

        public string MessageType { get; private set; }
        public IList<ValidationIssue> Issues { get; private set; }
    }
}

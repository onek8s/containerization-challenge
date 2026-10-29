namespace VanArsdel.Integration.Contracts
{
    /// <summary>
    /// Names and namespaces of the message context properties we read and write. The VanArsdel properties are
    /// defined in VanArsdel.Integration.Schemas.PropertySchema (biztalk\VanArsdel.Integration.Schemas).
    /// </summary>
    public static class ContextProperties
    {
        public const string VanArsdelNamespace = "https://VanArsdel.Integration.Schemas.PropertySchema";
        public const string SystemNamespace = "http://schemas.microsoft.com/BizTalk/2003/system-properties";
        public const string FileNamespace = "http://schemas.microsoft.com/BizTalk/2003/file-properties";
        public const string FtpNamespace = "http://schemas.microsoft.com/BizTalk/2003/ftp-properties";
        public const string SftpNamespace = "http://schemas.microsoft.com/BizTalk/2012/Adapter/sftp-properties";
        public const string EdiNamespace = "http://schemas.microsoft.com/Edi/PropertySchema";
        public const string EdiOverrideNamespace = "http://schemas.microsoft.com/BizTalk/2006/edi-properties";
        public const string As2Namespace = "http://schemas.microsoft.com/BizTalk/2006/as2-properties";
        public const string ErrorReportNamespace = "http://schemas.microsoft.com/BizTalk/2005/error-report";

        // VanArsdel property schema
        public const string PartnerId = "PartnerId";
        public const string MessageKind = "MessageKind";
        public const string OrderNumber = "OrderNumber";
        public const string RoutingSlip = "RoutingSlip";
        public const string InterchangeControlNumber = "InterchangeControlNumber";
        public const string AckControlNumber = "AckControlNumber";
        public const string CorrelationToken = "CorrelationToken";
        public const string ResubmitCount = "ResubmitCount";
        public const string ResubmittedBy = "ResubmittedBy";
        public const string SourceSystem = "SourceSystem";
        public const string ArchivePath = "ArchivePath";
        public const string BodyHash = "BodyHash";

        /// <summary>Logical send endpoint (AS2.C1001, SFTP.C1002, ...) the send port filters subscribe on.</summary>
        public const string Endpoint = "Endpoint";

        // BizTalk system properties
        public const string MessageType = "MessageType";
        public const string ReceivePortName = "ReceivePortName";
        public const string InboundTransportLocation = "InboundTransportLocation";
        public const string InterchangeID = "InterchangeID";
        public const string ReceivedFileName = "ReceivedFileName";
        public const string SchemaStrongName = "SchemaStrongName";
        public const string SuspendMessageOnRoutingFailure = "SuspendMessageOnRoutingFailure";

        // EDI properties promoted by the EDI disassembler
        public const string Isa06 = "ISA06";
        public const string Isa08 = "ISA08";
        public const string Isa13 = "ISA13";
        public const string UnbSenderId = "UNB2_1";
        public const string UnbRecipientId = "UNB3_1";
        public const string UnbReference = "UNB5";

        // AS2
        public const string As2From = "AS2From";
        public const string As2To = "AS2To";
        public const string As2MessageId = "MessageId";
    }

    /// <summary>MessageType values (namespace#root) of the documents the platform routes on.</summary>
    public static class MessageTypes
    {
        public const string CanonicalNamespace = "http://schemas.vanarsdel.com/integration/canonical/2014";
        public const string PartnerOrdersNamespace = "http://schemas.vanarsdel.com/partners/orders/2016";
        public const string X12Namespace = "http://schemas.microsoft.com/BizTalk/EDI/X12/2006";
        public const string EdifactNamespace = "http://schemas.microsoft.com/BizTalk/EDI/EDIFACT/2006";
        public const string IDocNamespace = "http://schemas.vanarsdel.com/erp/idoc/2013";

        public const string Order = CanonicalNamespace + "#Order";
        public const string Invoice = CanonicalNamespace + "#Invoice";
        public const string Despatch = CanonicalNamespace + "#Despatch";
        public const string FunctionalAck = CanonicalNamespace + "#FunctionalAck";
        public const string PartnerOrderBatch = PartnerOrdersNamespace + "#OrderBatch";
        public const string PartnerOrder = PartnerOrdersNamespace + "#Order";

        public const string X12_850 = X12Namespace + "#X12_00401_850";
        public const string X12_810 = X12Namespace + "#X12_00401_810";
        public const string X12_856 = X12Namespace + "#X12_00401_856";
        public const string X12_997 = X12Namespace + "#X12_00401_997";
        public const string Edifact_Orders = EdifactNamespace + "#EFACT_D96A_ORDERS";
        public const string Edifact_Invoic = EdifactNamespace + "#EFACT_D96A_INVOIC";
        public const string Edifact_Desadv = EdifactNamespace + "#EFACT_D96A_DESADV";
        public const string Edifact_Contrl = EdifactNamespace + "#EFACT_D3_CONTRL";

        public const string IDoc_Orders05 = IDocNamespace + "#ORDERS05";
        public const string IDoc_Desadv01 = IDocNamespace + "#DESADV01";
        public const string IDoc_Invoic02 = IDocNamespace + "#INVOIC02";

        public static string Of(string ns, string root)
        {
            return string.IsNullOrEmpty(ns) ? root : ns + "#" + root;
        }
    }
}

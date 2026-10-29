using System;

namespace Tailspin.DealerOrders.Domain
{
    public enum AxCommandType
    {
        SyncDealer = 0,
        RegisterQuote = 1,
        ConvertQuote = 2,
        CreateOrder = 3,
        UpdateOrder = 4,
        CancelOrder = 5,
        ReserveStock = 6,
        ReleaseReservation = 7,
        ReadOrder = 8
    }

    public enum AxCommandState
    {
        Pending = 0,
        Running = 1,
        Done = 2,
        Retry = 3,
        Failed = 4,
        Cancelled = 5
    }

    /// <summary>A unit of work for the AX connector service. Commands for one order run strictly in sequence.</summary>
    public class AxCommand
    {
        public long Id { get; set; }
        public AxCommandType CommandType { get; set; }
        public AxCommandState State { get; set; }
        public int? DealerId { get; set; }
        public int? QuoteId { get; set; }
        public int? OrderId { get; set; }

        /// <summary>Serialization key: commands with the same key are processed one at a time in Id order.</summary>
        public string SequenceKey { get; set; }

        public string Payload { get; set; }
        public string RequestedBy { get; set; }

        /// <summary>AX user the command runs as (LogonAs); empty runs as the connector's own identity.</summary>
        public string RunAsAxUser { get; set; }

        public DateTime CreatedUtc { get; set; }
        public DateTime NextAttemptUtc { get; set; }
        public int Attempts { get; set; }
        public DateTime? CompletedUtc { get; set; }
        public string LastError { get; set; }
        public string Result { get; set; }
    }

    public enum EdiTransport
    {
        As2 = 0,
        Sftp = 1
    }

    public class EdiPartner
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int DealerId { get; set; }
        public EdiTransport Transport { get; set; }

        /// <summary>ISA05/ISA06 of the dealer.</summary>
        public string InterchangeQualifier { get; set; }
        public string InterchangeId { get; set; }

        /// <summary>GS02 application sender code used by the dealer.</summary>
        public string ApplicationCode { get; set; }

        public string As2Id { get; set; }
        public string As2Url { get; set; }
        public string PartnerCertificateFile { get; set; }
        public bool EncryptOutbound { get; set; }
        public bool RequestSignedMdn { get; set; }

        public string SftpHost { get; set; }
        public int SftpPort { get; set; }
        public string SftpUser { get; set; }
        public string SftpInboundFolder { get; set; }
        public string SftpOutboundFolder { get; set; }

        public bool Send855 { get; set; }
        public bool Send856 { get; set; }
        public bool Active { get; set; }
        public int LastInterchangeControl { get; set; }
    }

    public enum EdiDirection
    {
        Inbound = 0,
        Outbound = 1
    }

    public enum EdiMessageState
    {
        Received = 0,
        Processed = 1,
        Rejected = 2,
        Queued = 3,
        Sent = 4,
        Acknowledged = 5,
        Failed = 6
    }

    public class EdiMessage
    {
        public int Id { get; set; }
        public int PartnerId { get; set; }
        public EdiDirection Direction { get; set; }
        public string TransactionSet { get; set; }
        public string InterchangeControl { get; set; }
        public string GroupControl { get; set; }
        public string TransactionControl { get; set; }
        public string Reference { get; set; }
        public EdiMessageState State { get; set; }
        public string Content { get; set; }
        public string As2MessageId { get; set; }
        public string MdnStatus { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? SentUtc { get; set; }
        public int Attempts { get; set; }
        public string Error { get; set; }
        public int? OrderId { get; set; }
    }

    public enum NotificationKind
    {
        QuoteApproved = 0,
        QuoteRejected = 1,
        OrderReceived = 2,
        OrderApproved = 3,
        OrderRejected = 4,
        OrderConfirmed = 5,
        DeliveryDateChanged = 6,
        OrderShipped = 7,
        OrderInvoiced = 8,
        OrderError = 9,
        ApprovalNeeded = 10,
        CreditHold = 11,
        PasswordReset = 12,
        DailyDigest = 13
    }

    public class Notification
    {
        public long Id { get; set; }
        public NotificationKind Kind { get; set; }
        public int? DealerId { get; set; }
        public int? OrderId { get; set; }
        public int? QuoteId { get; set; }
        public string Recipient { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }

        /// <summary>JSON payload pushed to the dealer portal (SignalR) in addition to mail.</summary>
        public string PushPayload { get; set; }

        public DateTime CreatedUtc { get; set; }
        public DateTime? MailSentUtc { get; set; }
        public DateTime? PushedUtc { get; set; }
        public int Attempts { get; set; }
        public string LastError { get; set; }
    }

    public enum DocumentKind
    {
        Quote = 0,
        OrderConfirmation = 1,
        DealerContract = 2,
        PriceList = 3,
        OrderExport = 4
    }

    public class StoredDocument
    {
        public int Id { get; set; }
        public DocumentKind Kind { get; set; }
        public int? DealerId { get; set; }
        public int? QuoteId { get; set; }
        public int? OrderId { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public byte[] Content { get; set; }
        public string Sha256 { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string CreatedBy { get; set; }
    }

    public static class DocumentNumbers
    {
        public static string Quote(int year, long sequence) => $"Q-{year}-{sequence:000000}";
        public static string Order(int year, long sequence) => $"WO-{year}-{sequence:000000}";
    }
}

using System;

namespace Wingtip.Invoices.Infrastructure.Messaging
{
    /// <summary>Body of the messages in the inbound queue.</summary>
    [Serializable]
    public class InboundFileMessage
    {
        public long FileId { get; set; }
        public int SupplierId { get; set; }
        public string SupplierCode { get; set; }
        public string FileName { get; set; }
        public string ArchivePath { get; set; }
        public DateTime ReceivedAt { get; set; }

        /// <summary>The file itself for files under the inline limit; otherwise read from ArchivePath.</summary>
        public byte[] Content { get; set; }

        public string ResubmittedBy { get; set; }
    }
}

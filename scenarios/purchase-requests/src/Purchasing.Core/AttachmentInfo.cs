using System;

namespace Contoso.Purchasing.Core
{
    public class AttachmentInfo
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public long SizeBytes { get; set; }
        public string UploadedBy { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}

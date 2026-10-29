using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Fabrikam.Housing.Documents;

namespace Fabrikam.Housing.Integration.DocumentStore
{
    public class DocumentUpload
    {
        public DocumentCategory Category { get; set; }
        public string Title { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public int? PropertyId { get; set; }
        public int? TenancyId { get; set; }
        public int? WorkOrderId { get; set; }
        public int? ContractorId { get; set; }
        public bool VisibleToTenant { get; set; }
        public string UploadedBy { get; set; }
    }

    public class DocumentContent
    {
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public byte[] Content { get; set; }
    }

    public class SearchHit
    {
        public Guid DocumentKey { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public string Snippet { get; set; }
        public float Score { get; set; }
        public int? PropertyId { get; set; }
        public int? TenancyId { get; set; }
        public DateTime UploadedAt { get; set; }
    }

    /// <summary>
    /// Client for the document service on docs01 (HTTP API with an API key).
    /// </summary>
    public interface IDocumentServiceClient
    {
        Task<DocumentRecord> UploadAsync(DocumentUpload upload, Stream content);
        Task<DocumentContent> DownloadAsync(Guid documentKey);
        Task<DocumentContent> ThumbnailAsync(Guid documentKey);
        Task<IList<SearchHit>> SearchAsync(string query, int? tenancyId = null, int? propertyId = null, int max = 50);
        Task DeleteAsync(Guid documentKey, string reason);
    }
}

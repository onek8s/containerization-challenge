using System;
using Fabrikam.Housing.Common;

namespace Fabrikam.Housing.Documents
{
    public enum DocumentCategory
    {
        TenancyAgreement = 1,
        Correspondence = 2,
        Letter = 3,
        Certificate = 4,
        RepairPhoto = 5,
        Invoice = 6,
        ScannedPost = 7,
        Notice = 8,
        Statement = 9,
        Identification = 10
    }

    /// <summary>
    /// Metadata for a file held by the document service. The content itself is only reachable through
    /// the document service API.
    /// </summary>
    public class DocumentRecord : Entity
    {
        public Guid DocumentKey { get; set; }
        public DocumentCategory Category { get; set; }
        public string Title { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public long SizeBytes { get; set; }
        public string Sha256 { get; set; }
        /// <summary>Path relative to the document store root, e.g. 2021\04\{key}.pdf</summary>
        public string StoragePath { get; set; }
        public string ThumbnailPath { get; set; }
        public int? PropertyId { get; set; }
        public int? TenancyId { get; set; }
        public int? WorkOrderId { get; set; }
        public int? ContractorId { get; set; }
        public bool VisibleToTenant { get; set; }
        public DateTime UploadedAt { get; set; }
        public string UploadedBy { get; set; }
        public DateTime? RetainUntil { get; set; }
        public DateTime? IndexedAt { get; set; }
        public bool Deleted { get; set; }
    }

    public static class RetentionSchedule
    {
        /// <summary>Retention per the Fabrikam data retention schedule DP-03.</summary>
        public static DateTime? RetainUntil(DocumentCategory category, DateTime created)
        {
            switch (category)
            {
                case DocumentCategory.TenancyAgreement: return null; // kept for the life of the tenancy + 6 years, set at tenancy end
                case DocumentCategory.Certificate: return created.AddYears(6);
                case DocumentCategory.Invoice: return created.AddYears(7);
                case DocumentCategory.RepairPhoto: return created.AddYears(2);
                case DocumentCategory.ScannedPost: return created.AddYears(2);
                case DocumentCategory.Identification: return created.AddMonths(6);
                default: return created.AddYears(6);
            }
        }
    }
}

using System;
using System.Collections.Generic;

namespace Northwind.Inspections.Core
{
    public enum InspectionStatus
    {
        Draft = 0,
        Submitted = 1,
        ReportQueued = 2,
        ReportSent = 3,
        Closed = 4
    }

    public enum PhotoSize
    {
        Full,
        Thumbnail
    }

    public class Adjuster
    {
        public int Id { get; set; }
        public string Login { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public string Company { get; set; }
        public string PasswordHash { get; set; }
        public bool IsActive { get; set; }
        public bool IsSupervisor { get; set; }
        public DateTime? LastLoginUtc { get; set; }
    }

    public class Inspection
    {
        public int Id { get; set; }
        public string ClaimNumber { get; set; }
        public string PolicyHolder { get; set; }
        public string PropertyAddress { get; set; }
        public string PerilCode { get; set; }
        public DateTime InspectedOn { get; set; }
        public int AdjusterId { get; set; }
        public string AdjusterName { get; set; }
        public string HandlerEmail { get; set; }
        public InspectionStatus Status { get; set; }
        public decimal? EstimatedLoss { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? ReportSentUtc { get; set; }
        public int PhotoCount { get; set; }
    }

    public class Photo
    {
        public Guid Id { get; set; }
        public int InspectionId { get; set; }
        public int Sequence { get; set; }
        public string OriginalFileName { get; set; }
        public string Caption { get; set; }
        public string StoragePath { get; set; }
        public string ThumbnailPath { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long Bytes { get; set; }
        public DateTime? TakenUtc { get; set; }
        public string UploadedBy { get; set; }
        public DateTime UploadedUtc { get; set; }
        public bool Purged { get; set; }
    }

    public class ReportRequest
    {
        public long Id { get; set; }
        public int InspectionId { get; set; }
        public string Recipient { get; set; }
        public string RequestedBy { get; set; }
        public DateTime RequestedUtc { get; set; }
        public int Attempts { get; set; }
        public string LastError { get; set; }
    }

    public class InspectionReport
    {
        public Inspection Inspection { get; set; }
        public IList<Photo> Photos { get; set; }
        public string ContactSheetContentId { get; set; }
        public DateTime GeneratedUtc { get; set; }
    }
}

using System;
using System.Collections.Generic;

namespace Tailspin.CustomerCare.Domain
{
    public enum CasePriority
    {
        Low = 1,
        Normal = 2,
        High = 3,
        Critical = 4
    }

    public enum CaseStatus
    {
        New = 0,
        Assigned = 1,
        InProgress = 2,
        WaitingOnCustomer = 3,
        Resolved = 4,
        Closed = 5
    }

    public enum CaseChannel
    {
        Phone = 1,
        Email = 2,
        Web = 3,
        Billing = 4,
        Partner = 5
    }

    public class Customer
    {
        public int Id { get; set; }
        public string AccountNumber { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Street { get; set; }
        public string PostalCode { get; set; }
        public string City { get; set; }
        /// <summary>RES (residential), SME or LCO (large commercial).</summary>
        public string Segment { get; set; }
        public bool IsPriorityCustomer { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime ModifiedUtc { get; set; }

        public virtual ICollection<Case> Cases { get; set; } = new List<Case>();
    }

    public class Case
    {
        public int Id { get; set; }
        public string CaseNumber { get; set; }
        public int CustomerId { get; set; }
        public virtual Customer Customer { get; set; }
        public int QueueId { get; set; }
        public virtual Queue Queue { get; set; }
        public string CategoryCode { get; set; }
        public string Subject { get; set; }
        public string Description { get; set; }
        public CasePriority Priority { get; set; }
        public CaseStatus Status { get; set; }
        public CaseChannel Channel { get; set; }
        public string AssignedTo { get; set; }
        public string ExternalReference { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string CreatedBy { get; set; }
        public DateTime ResponseDueUtc { get; set; }
        public DateTime ResolveDueUtc { get; set; }
        public DateTime? FirstResponseUtc { get; set; }
        public DateTime? ResolvedUtc { get; set; }
        public DateTime? ClosedUtc { get; set; }
        public int EscalationLevel { get; set; }
        public DateTime ModifiedUtc { get; set; }
        public byte[] RowVersion { get; set; }

        public virtual ICollection<CaseNote> Notes { get; set; } = new List<CaseNote>();
        public virtual ICollection<CaseAttachment> Attachments { get; set; } = new List<CaseAttachment>();

        public bool IsOpen
        {
            get { return Status != CaseStatus.Resolved && Status != CaseStatus.Closed; }
        }
    }

    public class CaseNote
    {
        public int Id { get; set; }
        public int CaseId { get; set; }
        public string Author { get; set; }
        public string Body { get; set; }
        public bool IsInternal { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public class CaseAttachment
    {
        public int Id { get; set; }
        public int CaseId { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public int Length { get; set; }
        public byte[] Content { get; set; }
        public string UploadedBy { get; set; }
        public DateTime UploadedUtc { get; set; }
    }

    public class Queue
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public CasePriority DefaultPriority { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
    }

    public class Category
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string QueueCode { get; set; }
        public bool IsActive { get; set; }
    }

    public class RoutingRule
    {
        public int Id { get; set; }
        public int Sequence { get; set; }
        public string CategoryCode { get; set; }
        public string PostalCodePrefix { get; set; }
        public string Segment { get; set; }
        public string QueueCode { get; set; }
    }

    public class SlaTarget
    {
        public CasePriority Priority { get; set; }
        public double ResponseHours { get; set; }
        public double ResolveHours { get; set; }
    }

    public class Holiday
    {
        public DateTime Date { get; set; }
        public string Name { get; set; }
    }

    public class KbArticle
    {
        public int Id { get; set; }
        public string Slug { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string Tags { get; set; }
        public string CategoryCode { get; set; }
        public bool IsPublished { get; set; }
        public int Views { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime ModifiedUtc { get; set; }
    }
}

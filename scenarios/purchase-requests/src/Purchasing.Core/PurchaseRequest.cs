using System;
using System.Collections.Generic;
using System.Linq;

namespace Contoso.Purchasing.Core
{
    public class PurchaseRequest
    {
        public PurchaseRequest()
        {
            Currency = "EUR";
            Lines = new List<RequestLine>();
            Approvals = new List<ApprovalRecord>();
            Attachments = new List<AttachmentInfo>();
        }

        public int Id { get; set; }
        public string Number { get; set; }
        public string RequesterAccount { get; set; }
        public string RequesterName { get; set; }
        public string RequesterEmail { get; set; }
        public string CostCenterCode { get; set; }
        public string CostCenterName { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; }
        public string Category { get; set; }
        public string Title { get; set; }
        public string Justification { get; set; }
        public DateTime? NeededBy { get; set; }
        public string Currency { get; set; }
        public RequestStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? LastReminderAt { get; set; }
        public string PoNumber { get; set; }

        public List<RequestLine> Lines { get; set; }
        public List<ApprovalRecord> Approvals { get; set; }
        public List<AttachmentInfo> Attachments { get; set; }

        public decimal NetTotal
        {
            get { return Lines.Sum(l => l.NetAmount); }
        }

        public decimal VatTotal
        {
            get { return Lines.Sum(l => l.VatAmount); }
        }

        public decimal GrossTotal
        {
            get { return NetTotal + VatTotal; }
        }

        public DateTime? LastActivityAt
        {
            get
            {
                var last = Approvals.Count == 0 ? (DateTime?)null : Approvals.Max(a => a.DecidedAt);
                if (last == null) return SubmittedAt;
                if (SubmittedAt == null) return last;
                return last > SubmittedAt ? last : SubmittedAt;
            }
        }
    }
}

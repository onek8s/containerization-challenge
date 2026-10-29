using System;
using System.Collections.Generic;
using Contoso.Purchasing.Core;

namespace Contoso.Purchasing.Api.Models
{
    public class RequestInput
    {
        public string CostCenterCode { get; set; }
        public int SupplierId { get; set; }
        public string Category { get; set; }
        public string Title { get; set; }
        public string Justification { get; set; }
        public DateTime? NeededBy { get; set; }
        public List<RequestLine> Lines { get; set; }
    }

    public class DecisionInput
    {
        public bool Approve { get; set; }
        public string Comment { get; set; }
    }

    public class OrderInput
    {
        public int RequestId { get; set; }
        public string DeliveryAddress { get; set; }
    }

    public class RequestView
    {
        public PurchaseRequest Request { get; set; }
        public IList<ApprovalStep> RequiredSteps { get; set; }
        public ApprovalStep? NextStep { get; set; }
        public bool CanDecide { get; set; }
        public bool CanEdit { get; set; }
        public bool CanOrder { get; set; }
    }

    public class RequestSummary
    {
        public int Id { get; set; }
        public string Number { get; set; }
        public string Title { get; set; }
        public string RequesterName { get; set; }
        public string CostCenterCode { get; set; }
        public string SupplierName { get; set; }
        public RequestStatus Status { get; set; }
        public ApprovalStep? NextStep { get; set; }
        public decimal NetTotal { get; set; }
        public string Currency { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public string PoNumber { get; set; }
    }
}

using System;
using System.Collections.Generic;

namespace ProductReturns.Core.Api
{
    public class OrderLookupResponse
    {
        public string OrderNumber { get; set; }
        public string CustomerName { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime? DeliveredDate { get; set; }
        public string Currency { get; set; }
        public List<ReturnableLine> Lines { get; set; } = new List<ReturnableLine>();
    }

    public class CreateReturnLine
    {
        public int LineNumber { get; set; }
        public int Quantity { get; set; }
        public string ReasonCode { get; set; }
        public string Comment { get; set; }
    }

    public class CreateReturnRequest
    {
        public string OrderNumber { get; set; }
        public string Email { get; set; }
        public List<CreateReturnLine> Lines { get; set; } = new List<CreateReturnLine>();
    }

    public class CreateReturnResponse
    {
        public string RmaNumber { get; set; }
        public ReturnStatus Status { get; set; }
    }

    public class DecisionRequest
    {
        public string Reason { get; set; }
    }

    public class NoteRequest
    {
        public string Text { get; set; }
    }

    public class ScanRequest
    {
        public string Sku { get; set; }
        public int Quantity { get; set; } = 1;
        public ItemCondition Condition { get; set; }
    }

    public class ReturnSummary
    {
        public string RmaNumber { get; set; }
        public string OrderNumber { get; set; }
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public ReturnStatus Status { get; set; }
        public DateTime CreatedUtc { get; set; }
        public int Items { get; set; }
        public decimal Value { get; set; }
        public string TrackingNumber { get; set; }
    }

    public class ReturnSearchResponse
    {
        public List<ReturnSummary> Items { get; set; } = new List<ReturnSummary>();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class ReturnDetail
    {
        public string RmaNumber { get; set; }
        public string OrderNumber { get; set; }
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public string Currency { get; set; }
        public ReturnStatus Status { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? DecidedUtc { get; set; }
        public string DecidedBy { get; set; }
        public string RejectReason { get; set; }
        public string TrackingNumber { get; set; }
        public bool HasLabel { get; set; }
        public DateTime? LabelCreatedUtc { get; set; }
        public string LabelError { get; set; }
        public DateTime? ReceivedUtc { get; set; }
        public DateTime? InspectedUtc { get; set; }
        public string InspectedBy { get; set; }
        public decimal? RestockingFee { get; set; }
        public decimal? LabelFee { get; set; }
        public decimal? RefundAmount { get; set; }
        public DateTime? RefundExportedUtc { get; set; }
        public string RefundBatch { get; set; }
        public List<ReturnLineDetail> Lines { get; set; } = new List<ReturnLineDetail>();
        public List<ReturnNoteDetail> Notes { get; set; } = new List<ReturnNoteDetail>();
    }

    public class ReturnLineDetail
    {
        public int Id { get; set; }
        public int OrderLineNumber { get; set; }
        public string Sku { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string ReasonCode { get; set; }
        public string CustomerComment { get; set; }
        public int ReceivedQuantity { get; set; }
        public ItemCondition Condition { get; set; }
        public decimal? RefundAmount { get; set; }
    }

    public class ReturnNoteDetail
    {
        public string Author { get; set; }
        public string Text { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public class ScanResponse
    {
        public string RmaNumber { get; set; }
        public bool Accepted { get; set; }
        public bool Complete { get; set; }
        public string Message { get; set; }
        public List<ReturnLineDetail> Lines { get; set; } = new List<ReturnLineDetail>();
    }

    public static class ReturnMapping
    {
        public static ReturnDetail ToDetail(this ReturnRequest r)
        {
            var detail = new ReturnDetail
            {
                RmaNumber = r.RmaNumber,
                OrderNumber = r.OrderNumber,
                CustomerName = r.CustomerName,
                CustomerEmail = r.CustomerEmail,
                Currency = r.Currency,
                Status = r.Status,
                CreatedUtc = r.CreatedUtc,
                CreatedBy = r.CreatedBy,
                DecidedUtc = r.DecidedUtc,
                DecidedBy = r.DecidedBy,
                RejectReason = r.RejectReason,
                TrackingNumber = r.TrackingNumber,
                HasLabel = !string.IsNullOrEmpty(r.LabelPath),
                LabelCreatedUtc = r.LabelCreatedUtc,
                LabelError = r.LabelError,
                ReceivedUtc = r.ReceivedUtc,
                InspectedUtc = r.InspectedUtc,
                InspectedBy = r.InspectedBy,
                RestockingFee = r.RestockingFee,
                LabelFee = r.LabelFee,
                RefundAmount = r.RefundAmount,
                RefundExportedUtc = r.RefundExportedUtc,
                RefundBatch = r.RefundBatch
            };

            foreach (var l in r.Lines)
            {
                detail.Lines.Add(l.ToDetail());
            }

            foreach (var n in r.Notes)
            {
                detail.Notes.Add(new ReturnNoteDetail { Author = n.Author, Text = n.Text, CreatedUtc = n.CreatedUtc });
            }

            detail.Lines.Sort((a, b) => a.OrderLineNumber.CompareTo(b.OrderLineNumber));
            detail.Notes.Sort((a, b) => a.CreatedUtc.CompareTo(b.CreatedUtc));
            return detail;
        }

        public static ReturnLineDetail ToDetail(this ReturnLine l) => new ReturnLineDetail
        {
            Id = l.Id,
            OrderLineNumber = l.OrderLineNumber,
            Sku = l.Sku,
            Description = l.Description,
            Category = l.Category,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            ReasonCode = l.ReasonCode,
            CustomerComment = l.CustomerComment,
            ReceivedQuantity = l.ReceivedQuantity,
            Condition = l.Condition,
            RefundAmount = l.RefundAmount
        };
    }
}

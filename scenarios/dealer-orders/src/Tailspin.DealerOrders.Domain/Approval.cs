using System;

namespace Tailspin.DealerOrders.Domain
{
    /// <summary>A request for the sales desk to approve a quote or an order.</summary>
    public class ApprovalRequest
    {
        public int Id { get; set; }
        public int? QuoteId { get; set; }
        public int? OrderId { get; set; }
        public int DealerId { get; set; }
        public ApprovalReason Reason { get; set; }
        public string Details { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public decimal RequestedDiscount { get; set; }
        public DateTime RequestedUtc { get; set; }
        public string RequestedBy { get; set; }
        public ApprovalDecision Decision { get; set; }
        public string DecidedBy { get; set; }
        public DateTime? DecidedUtc { get; set; }
        public string DecisionComment { get; set; }
        public decimal? ApprovedDiscount { get; set; }

        /// <summary>Approvals over this amount (EUR) need a sales manager, not only a desk user.</summary>
        public const decimal ManagerApprovalThresholdEur = 250000m;

        public bool RequiresManager(decimal amountEur) =>
            Reason == ApprovalReason.CreditLimitExceeded || Reason == ApprovalReason.ExportControl || amountEur >= ManagerApprovalThresholdEur ||
            RequestedDiscount > 10m;
    }
}

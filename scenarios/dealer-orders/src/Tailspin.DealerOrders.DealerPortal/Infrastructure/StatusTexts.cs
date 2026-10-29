using Tailspin.DealerOrders.Domain;

namespace Tailspin.DealerOrders.DealerPortal.Infrastructure
{
    /// <summary>Status names as dealers see them (internal AX steps are folded together).</summary>
    public static class StatusTexts
    {
        public static string Order(OrderStatus status)
        {
            switch (status)
            {
                case OrderStatus.Draft: return "Draft";
                case OrderStatus.Submitted:
                case OrderStatus.Approved:
                case OrderStatus.SentToAx:
                case OrderStatus.CreatedInAx: return "Received";
                case OrderStatus.PendingApproval: return "Waiting for approval";
                case OrderStatus.CreditHold: return "On hold";
                case OrderStatus.Confirmed: return "Confirmed";
                case OrderStatus.InProduction: return "In production";
                case OrderStatus.PartiallyShipped: return "Partly shipped";
                case OrderStatus.Shipped: return "Shipped";
                case OrderStatus.Invoiced: return "Invoiced";
                case OrderStatus.Cancelled: return "Cancelled";
                case OrderStatus.Rejected: return "Rejected";
                case OrderStatus.Error: return "Received";
                default: return status.ToString();
            }
        }

        public static string Quote(QuoteStatus status) => status == QuoteStatus.PendingApproval ? "Waiting for approval" : status.ToString();

        public static string Css(OrderStatus status)
        {
            switch (status)
            {
                case OrderStatus.Rejected:
                case OrderStatus.Cancelled: return "label-default";
                case OrderStatus.CreditHold:
                case OrderStatus.PendingApproval: return "label-warning";
                case OrderStatus.Shipped:
                case OrderStatus.Invoiced: return "label-success";
                default: return "label-info";
            }
        }
    }
}

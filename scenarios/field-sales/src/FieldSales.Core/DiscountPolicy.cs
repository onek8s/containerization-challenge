namespace Wingtip.FieldSales.Core
{
    public enum DiscountDecision
    {
        WithinLimit,
        NeedsApproval,
        NotAllowed
    }

    /// <summary>
    /// Sales reps may give manual line discounts up to their own limit (stored per user). Anything above goes to a
    /// sales manager for approval. Line discounts above the company maximum are never accepted.
    /// </summary>
    public class DiscountPolicy
    {
        public const decimal CompanyMaximumPercent = 25m;

        public DiscountDecision Evaluate(SalesOrder order, decimal userLimitPercent)
        {
            var max = order.MaxLineDiscountPercent;
            if (max > CompanyMaximumPercent) return DiscountDecision.NotAllowed;
            if (max > userLimitPercent) return DiscountDecision.NeedsApproval;
            return DiscountDecision.WithinLimit;
        }
    }
}

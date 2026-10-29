namespace Wingtip.Invoices.Core.Matching
{
    public class MatchingPolicy
    {
        public MatchingPolicy()
        {
            PriceTolerancePercent = 2m;
            TotalToleranceAmount = 5m;
            AutoApproveLimit = 25000m;
        }

        /// <summary>Allowed unit price difference against the order, in percent.</summary>
        public decimal PriceTolerancePercent { get; set; }

        /// <summary>Allowed difference of the invoice net total against the matched order value.</summary>
        public decimal TotalToleranceAmount { get; set; }

        /// <summary>Invoices above this net amount always need a person to release them.</summary>
        public decimal AutoApproveLimit { get; set; }

        /// <summary>Allow invoicing ordered but not yet received quantities (two-way match).</summary>
        public bool TwoWayMatch { get; set; }
    }
}

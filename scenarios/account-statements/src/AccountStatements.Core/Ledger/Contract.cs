using System;

namespace Woodgrove.Statements.Core.Ledger
{
    public class LeaseContract
    {
        public int ContractId { get; set; }
        public string ContractNo { get; set; }
        public int CustomerId { get; set; }
        public string AssetCategory { get; set; }
        public string AssetDescription { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal MonthlyRent { get; set; }
        public decimal AssetValue { get; set; }
        public decimal ResidualValue { get; set; }
        public string Status { get; set; }

        public int RemainingMonths(DateTime asOf)
        {
            if (asOf >= EndDate) return 0;
            var start = asOf < StartDate ? StartDate : asOf;
            var months = (EndDate.Year - start.Year) * 12 + EndDate.Month - start.Month;
            if (EndDate.Day > start.Day) months++;
            return Math.Max(0, months);
        }

        public decimal RemainingRent(DateTime asOf)
        {
            return RemainingMonths(asOf) * MonthlyRent;
        }
    }
}

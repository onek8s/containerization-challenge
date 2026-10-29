using System;
using System.Collections.Generic;
using System.Linq;
using Woodgrove.Statements.Core.Ledger;

namespace Woodgrove.Statements.Core.Statements
{
    public class PortfolioRow
    {
        public string AssetCategory { get; set; }
        public string ContractNo { get; set; }
        public string CustomerNo { get; set; }
        public string CustomerName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal MonthlyRent { get; set; }
        public decimal AssetValue { get; set; }
        public decimal ResidualValue { get; set; }
        public int RemainingMonths { get; set; }
        public decimal RemainingRent { get; set; }
        public string EndBand { get; set; }
    }

    public static class Portfolio
    {
        public static List<PortfolioRow> Build(IEnumerable<LeaseContract> contracts, IDictionary<int, Customer> customers, DateTime asOf)
        {
            var rows = new List<PortfolioRow>();
            foreach (var c in contracts.Where(c => c.StartDate <= asOf && c.EndDate >= asOf && c.Status != "TERMINATED"))
            {
                Customer customer;
                customers.TryGetValue(c.CustomerId, out customer);
                var remaining = c.RemainingMonths(asOf);
                rows.Add(new PortfolioRow
                {
                    AssetCategory = c.AssetCategory,
                    ContractNo = c.ContractNo,
                    CustomerNo = customer?.CustomerNo,
                    CustomerName = customer?.Name,
                    StartDate = c.StartDate,
                    EndDate = c.EndDate,
                    MonthlyRent = c.MonthlyRent,
                    AssetValue = c.AssetValue,
                    ResidualValue = c.ResidualValue,
                    RemainingMonths = remaining,
                    RemainingRent = c.RemainingRent(asOf),
                    EndBand = EndBand(remaining)
                });
            }
            return rows.OrderBy(r => r.AssetCategory, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.EndDate).ToList();
        }

        public static string EndBand(int remainingMonths)
        {
            if (remainingMonths <= 6) return "0-6 months";
            if (remainingMonths <= 12) return "7-12 months";
            if (remainingMonths <= 24) return "13-24 months";
            if (remainingMonths <= 48) return "25-48 months";
            return "over 48 months";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Woodgrove.Statements.Core.Ledger;

namespace Woodgrove.Statements.Core.Statements
{
    public class AgedDebtRow
    {
        public string CreditController { get; set; }
        public string CustomerNo { get; set; }
        public string CustomerName { get; set; }
        public decimal CreditLimit { get; set; }
        public bool OnHold { get; set; }
        public decimal NotDue { get; set; }
        public decimal Days1To30 { get; set; }
        public decimal Days31To60 { get; set; }
        public decimal Days61To90 { get; set; }
        public decimal Over90 { get; set; }
        public decimal Unallocated { get; set; }
        public decimal Total { get; set; }
        public int DunningLevel { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public decimal LastPaymentAmount { get; set; }
    }

    public static class AgedDebt
    {
        public static List<AgedDebtRow> Build(IEnumerable<Customer> customers, IEnumerable<LedgerEntry> entries, DateTime asOf, bool includeZero = false)
        {
            var builder = new StatementBuilder();
            var byCustomer = entries.ToLookup(e => e.CustomerId);
            var rows = new List<AgedDebtRow>();
            foreach (var c in customers)
            {
                var own = byCustomer[c.CustomerId].ToList();
                var st = builder.Build(c, own, asOf, asOf);
                if (!includeZero && st.OpenItems.Count == 0) continue;
                var lastPayment = own.Where(e => e.EntryType == EntryTypes.Payment && e.PostingDate.Date <= asOf.Date)
                                     .OrderByDescending(e => e.PostingDate).FirstOrDefault();
                rows.Add(new AgedDebtRow
                {
                    CreditController = string.IsNullOrEmpty(c.CreditController) ? "(unassigned)" : c.CreditController,
                    CustomerNo = c.CustomerNo,
                    CustomerName = c.Name,
                    CreditLimit = c.CreditLimit,
                    OnHold = c.OnHold,
                    NotDue = st.Aging.NotDue,
                    Days1To30 = st.Aging.Days1To30,
                    Days31To60 = st.Aging.Days31To60,
                    Days61To90 = st.Aging.Days61To90,
                    Over90 = st.Aging.Over90,
                    Unallocated = st.Aging.Unallocated,
                    Total = st.Aging.Total,
                    DunningLevel = st.DunningLevel,
                    LastPaymentDate = lastPayment?.PostingDate,
                    LastPaymentAmount = lastPayment == null ? 0m : -lastPayment.Amount
                });
            }
            return rows.OrderBy(r => r.CreditController, StringComparer.OrdinalIgnoreCase)
                       .ThenByDescending(r => r.Total).ToList();
        }
    }
}

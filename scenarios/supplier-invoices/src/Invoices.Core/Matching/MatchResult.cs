using System.Collections.Generic;
using System.Linq;

namespace Wingtip.Invoices.Core.Matching
{
    public class LineMatch
    {
        public int InvoiceLine { get; set; }
        public int? OrderLine { get; set; }
        public decimal ExpectedAmount { get; set; }
        public string Account { get; set; }
    }

    public class MatchResult
    {
        public MatchResult()
        {
            Issues = new List<Issue>();
            Lines = new List<LineMatch>();
        }

        public IList<Issue> Issues { get; private set; }
        public IList<LineMatch> Lines { get; private set; }
        public decimal ExpectedNetAmount { get; set; }

        public bool Matched
        {
            get { return Issues.Count == 0; }
        }

        public InvoiceStatus Status
        {
            get { return Matched ? InvoiceStatus.Matched : InvoiceStatus.Exception; }
        }

        public string Summary
        {
            get { return Matched ? "Matched" : string.Join("; ", Issues.Select(i => i.Code).Distinct()); }
        }
    }
}

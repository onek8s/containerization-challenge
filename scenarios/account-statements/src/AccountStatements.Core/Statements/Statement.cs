using System;
using System.Collections.Generic;
using Woodgrove.Statements.Core.Ledger;

namespace Woodgrove.Statements.Core.Statements
{
    public class Statement
    {
        public Customer Customer { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime StatementDate { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal ClosingBalance { get; set; }
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public List<StatementLine> Lines { get; set; } = new List<StatementLine>();
        public List<OpenItem> OpenItems { get; set; } = new List<OpenItem>();
        public AgingBuckets Aging { get; set; } = new AgingBuckets();
        public int DunningLevel { get; set; }
        public string StatementNo { get; set; }

        public bool HasActivity
        {
            get { return Lines.Count > 0 || OpenItems.Count > 0 || OpeningBalance != 0m; }
        }
    }

    public class StatementLine
    {
        public DateTime PostingDate { get; set; }
        public string EntryType { get; set; }
        public string DocumentNo { get; set; }
        public string ContractNo { get; set; }
        public string Description { get; set; }
        public DateTime? DueDate { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal Balance { get; set; }
    }

    public class OpenItem
    {
        public string EntryType { get; set; }
        public string DocumentNo { get; set; }
        public string ContractNo { get; set; }
        public DateTime PostingDate { get; set; }
        public DateTime DueDate { get; set; }
        public decimal OriginalAmount { get; set; }
        public decimal OpenAmount { get; set; }
        public int DaysOverdue { get; set; }
        public string Bucket { get; set; }
        public string PaymentReference { get; set; }
    }

    public class AgingBuckets
    {
        public decimal NotDue { get; set; }
        public decimal Days1To30 { get; set; }
        public decimal Days31To60 { get; set; }
        public decimal Days61To90 { get; set; }
        public decimal Over90 { get; set; }
        public decimal Unallocated { get; set; }

        public decimal Overdue
        {
            get { return Days1To30 + Days31To60 + Days61To90 + Over90; }
        }

        public decimal Total
        {
            get { return NotDue + Overdue + Unallocated; }
        }

        public void Add(AgingBuckets other)
        {
            NotDue += other.NotDue;
            Days1To30 += other.Days1To30;
            Days31To60 += other.Days31To60;
            Days61To90 += other.Days61To90;
            Over90 += other.Over90;
            Unallocated += other.Unallocated;
        }
    }
}

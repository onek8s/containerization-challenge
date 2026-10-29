using System;
using System.Collections.Generic;

namespace Woodgrove.Statements.Api.Models
{
    public class StatementRequest
    {
        public string CustomerNo { get; set; }
        public DateTime? PeriodStart { get; set; }
        public DateTime? StatementDate { get; set; }
        /// <summary>pdf (default), xlsx or docx</summary>
        public string Format { get; set; }
    }

    public class ReportRequest
    {
        public Dictionary<string, string> Parameters { get; set; }
        public string Format { get; set; }
    }

    public class ReportInfo
    {
        public string Code { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public IList<string> Parameters { get; set; }
        public IList<string> Formats { get; set; }
    }

    public class CustomerBalanceInfo
    {
        public string CustomerNo { get; set; }
        public string Name { get; set; }
        public decimal OpenBalance { get; set; }
        public decimal Overdue { get; set; }
        public decimal NotDue { get; set; }
        public decimal Days1To30 { get; set; }
        public decimal Days31To60 { get; set; }
        public decimal Days61To90 { get; set; }
        public decimal Over90 { get; set; }
        public decimal Unallocated { get; set; }
        public int DunningLevel { get; set; }
        public bool OnHold { get; set; }
        public DateTime LedgerDate { get; set; }
    }
}

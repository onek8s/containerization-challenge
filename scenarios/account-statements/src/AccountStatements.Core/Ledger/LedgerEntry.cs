using System;

namespace Woodgrove.Statements.Core.Ledger
{
    public static class EntryTypes
    {
        public const string Invoice = "INV";
        public const string CreditNote = "CRN";
        public const string Payment = "PAY";
        public const string Adjustment = "ADJ";
        public const string Interest = "INT";

        public static bool IsDebit(string type)
        {
            return type == Invoice || type == Interest;
        }
    }

    /// <summary>One open item or settled item of the accounts receivable ledger (loaded nightly from the ERP).</summary>
    public class LedgerEntry
    {
        public long EntryId { get; set; }
        public int CustomerId { get; set; }
        public string EntryType { get; set; }
        public string DocumentNo { get; set; }
        public string ContractNo { get; set; }
        public DateTime PostingDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string Description { get; set; }
        /// <summary>Signed amount: invoices and interest positive, payments and credit notes negative.</summary>
        public decimal Amount { get; set; }
        /// <summary>Remaining open amount at load time, signed like <see cref="Amount"/>.</summary>
        public decimal OpenAmount { get; set; }
        public DateTime? ClosedDate { get; set; }
        public string PaymentReference { get; set; }
    }
}

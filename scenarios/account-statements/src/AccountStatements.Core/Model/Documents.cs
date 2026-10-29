using System;
using System.Collections.Generic;

namespace Woodgrove.Statements.Core.Model
{
    public class CompanyProfile
    {
        public string CompanyName { get; set; }
        public string AddressLine1 { get; set; }
        public string PostalCode { get; set; }
        public string City { get; set; }
        public string BusinessId { get; set; }
        public string VatNumber { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Web { get; set; }
        public string BankName { get; set; }
        public string Iban { get; set; }
        public string Bic { get; set; }
        public byte[] Logo { get; set; }

        public string FormattedIban
        {
            get
            {
                if (string.IsNullOrEmpty(Iban)) return Iban;
                var compact = Iban.Replace(" ", "");
                var parts = new List<string>();
                for (var i = 0; i < compact.Length; i += 4)
                    parts.Add(compact.Substring(i, Math.Min(4, compact.Length - i)));
                return string.Join(" ", parts);
            }
        }
    }

    public class Invoice
    {
        public int InvoiceId { get; set; }
        public string InvoiceNo { get; set; }
        public int CustomerId { get; set; }
        public string ContractNo { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? PeriodFrom { get; set; }
        public DateTime? PeriodTo { get; set; }
        public decimal NetAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentReference { get; set; }
        public string CurrencyCode { get; set; }
        public string CustomerReference { get; set; }
        public List<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
    }

    public class InvoiceLine
    {
        public int InvoiceId { get; set; }
        public short LineNumber { get; set; }
        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal VatRate { get; set; }
        public decimal NetAmount { get; set; }
        public decimal VatAmount { get; set; }
    }

    public class LedgerLoad
    {
        public DateTime LoadedAt { get; set; }
        public DateTime LedgerDate { get; set; }
        public int Entries { get; set; }
        public string Source { get; set; }
    }

    public class CustomerBalance
    {
        public int CustomerId { get; set; }
        public string CustomerNo { get; set; }
        public string Name { get; set; }
        public string CreditController { get; set; }
        public decimal CreditLimit { get; set; }
        public bool OnHold { get; set; }
        public byte Delivery { get; set; }
        public decimal OpenBalance { get; set; }
        public decimal Overdue { get; set; }
        public DateTime? LastPaymentDate { get; set; }
    }

    public class TransactionRow
    {
        public DateTime PostingDate { get; set; }
        public string CustomerNo { get; set; }
        public string CustomerName { get; set; }
        public string CreditController { get; set; }
        public string EntryType { get; set; }
        public string DocumentNo { get; set; }
        public string ContractNo { get; set; }
        public DateTime? DueDate { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public decimal OpenAmount { get; set; }
        public string PaymentReference { get; set; }
    }
}

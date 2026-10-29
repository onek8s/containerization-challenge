using System;
using System.Collections.Generic;
using System.Linq;
using Woodgrove.Statements.Core.Ledger;
using Woodgrove.Statements.Core.Model;
using Woodgrove.Statements.Core.Statements;

namespace Woodgrove.Statements.Reporting.Data
{
    // Flat row types bound to the report datasets (see the DataSets of the .rdlc files).

    public class CompanyRow
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

        public static CompanyRow From(CompanyProfile p)
        {
            return new CompanyRow
            {
                CompanyName = p.CompanyName, AddressLine1 = p.AddressLine1, PostalCode = p.PostalCode, City = p.City,
                BusinessId = p.BusinessId, VatNumber = p.VatNumber, Phone = p.Phone, Email = p.Email, Web = p.Web,
                BankName = p.BankName, Iban = p.FormattedIban, Bic = p.Bic
            };
        }
    }

    public class StatementHeaderRow
    {
        public string CustomerNo { get; set; }
        public string CustomerName { get; set; }
        public string AddressBlock { get; set; }
        public string Language { get; set; }
        public string CurrencyCode { get; set; }
        public string StatementNo { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime StatementDate { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal ClosingBalance { get; set; }
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public decimal OverdueAmount { get; set; }
        public decimal DueAmount { get; set; }
        public DateTime PaymentDueDate { get; set; }
        public string PaymentReference { get; set; }
        public string VirtualBarcode { get; set; }
        public byte[] BarcodeImage { get; set; }
        public int DunningLevel { get; set; }
        public string CreditController { get; set; }
    }

    public class StatementLineRow
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

        public static List<StatementLineRow> From(IEnumerable<StatementLine> lines)
        {
            return lines.Select(l => new StatementLineRow
            {
                PostingDate = l.PostingDate, EntryType = l.EntryType, DocumentNo = l.DocumentNo, ContractNo = l.ContractNo,
                Description = l.Description, DueDate = l.DueDate, Debit = l.Debit, Credit = l.Credit, Balance = l.Balance
            }).ToList();
        }
    }

    public class OpenItemRow
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
        public int BucketOrder { get; set; }

        static readonly string[] Order = { AgingBucketNames.NotDue, AgingBucketNames.Days1To30, AgingBucketNames.Days31To60,
                                           AgingBucketNames.Days61To90, AgingBucketNames.Over90, AgingBucketNames.Unallocated };

        public static List<OpenItemRow> From(IEnumerable<OpenItem> items)
        {
            return items.Select(i => new OpenItemRow
            {
                EntryType = i.EntryType, DocumentNo = i.DocumentNo, ContractNo = i.ContractNo, PostingDate = i.PostingDate, DueDate = i.DueDate,
                OriginalAmount = i.OriginalAmount, OpenAmount = i.OpenAmount, DaysOverdue = i.DaysOverdue, Bucket = i.Bucket,
                BucketOrder = Array.IndexOf(Order, i.Bucket)
            }).ToList();
        }
    }

    public class InvoiceHeaderRow
    {
        public string InvoiceNo { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? PeriodFrom { get; set; }
        public DateTime? PeriodTo { get; set; }
        public string CustomerNo { get; set; }
        public string CustomerName { get; set; }
        public string AddressBlock { get; set; }
        public string ContractNo { get; set; }
        public string AssetDescription { get; set; }
        public string CustomerReference { get; set; }
        public decimal NetAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentReference { get; set; }
        public string CurrencyCode { get; set; }
        public string VirtualBarcode { get; set; }
        public byte[] BarcodeImage { get; set; }
        public bool ReverseCharge { get; set; }
        public bool IsCopy { get; set; }
    }

    public class InvoiceLineRow
    {
        public int LineNumber { get; set; }
        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal VatRate { get; set; }
        public decimal NetAmount { get; set; }
        public decimal VatAmount { get; set; }

        public static List<InvoiceLineRow> From(IEnumerable<InvoiceLine> lines)
        {
            return lines.Select(l => new InvoiceLineRow
            {
                LineNumber = l.LineNumber, Description = l.Description, Quantity = l.Quantity, UnitPrice = l.UnitPrice,
                VatRate = l.VatRate, NetAmount = l.NetAmount, VatAmount = l.VatAmount
            }).ToList();
        }
    }

    public static class AddressFormatter
    {
        public static string Block(Customer customer)
        {
            return string.Join("\r\n", customer.AddressLines());
        }
    }
}

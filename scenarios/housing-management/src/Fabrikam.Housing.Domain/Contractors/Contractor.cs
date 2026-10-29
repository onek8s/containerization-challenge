using System;
using System.Collections.Generic;
using System.Linq;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Repairs;

namespace Fabrikam.Housing.Contractors
{
    public class Contractor : AuditedEntity
    {
        public string Code { get; set; }
        public string Name { get; set; }
        /// <summary>Comma separated trades, e.g. "Plumbing,Heating".</summary>
        public string Trades { get; set; }
        /// <summary>Comma separated patch codes or "*" for all patches.</summary>
        public string Patches { get; set; }
        public bool OutOfHours { get; set; }
        public bool GasSafeRegistered { get; set; }
        public string GasSafeNumber { get; set; }
        public int WeeklyCapacity { get; set; }
        public string ContactEmail { get; set; }
        public string ContactPhone { get; set; }
        public string RemittanceEmail { get; set; }
        public bool VatRegistered { get; set; }
        public string VatNumber { get; set; }
        public string BankSortCode { get; set; }
        public string BankAccountNumber { get; set; }
        public string BankAccountName { get; set; }
        /// <summary>OAuth client id the contractor uses against the contractor API.</summary>
        public string ApiClientId { get; set; }
        /// <summary>SHA-256 of the client secret, base64 (IdentityServer format).</summary>
        public string ApiClientSecretHash { get; set; }
        public DateTime? ApiSecretIssuedAt { get; set; }
        public bool Active { get; set; } = true;

        public IEnumerable<Trade> TradeList =>
            (Trades ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => (Trade)Enum.Parse(typeof(Trade), t.Trim(), true));

        public bool CoversTrade(Trade trade) => TradeList.Contains(trade);

        public bool CoversPatch(string patch) =>
            Patches == "*" || (Patches ?? "").Split(',').Any(p => string.Equals(p.Trim(), patch, StringComparison.OrdinalIgnoreCase));
    }

    public enum InvoiceStatus
    {
        Received = 1,
        Approved = 2,
        Queried = 3,
        Rejected = 4,
        InPaymentRun = 5,
        Paid = 6
    }

    public class ContractorInvoice : AuditedEntity
    {
        public int ContractorId { get; set; }
        public Contractor Contractor { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public int WorkOrderId { get; set; }
        public WorkOrder WorkOrder { get; set; }
        public decimal NetAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal GrossAmount => NetAmount + VatAmount;
        public InvoiceStatus Status { get; set; }
        public string QueryReason { get; set; }
        public string ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int? PaymentRunId { get; set; }
        public int? JournalBatchId { get; set; }
    }

    public class PaymentRun : Entity
    {
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public DateTime ProcessingDate { get; set; }
        public string FileName { get; set; }
        public int PaymentCount { get; set; }
        public decimal Total { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public string Status { get; set; }
    }

    /// <summary>
    /// Checks a contractor invoice against the work order: the net amount may exceed the priced Schedule of
    /// Rates lines by at most 10% or £50 (whichever is lower) before it is queried with the contractor.
    /// </summary>
    public static class InvoiceMatching
    {
        public const decimal VatRate = 0.20m;

        public static string Check(ContractorInvoice invoice, WorkOrder order, Contractor contractor)
        {
            if (order == null) return "Work order not found";
            if (order.ContractorId != invoice.ContractorId) return "Work order is not assigned to this contractor";
            if (order.Status != WorkOrderStatus.Completed) return "Work order is not completed";
            var expected = order.Lines.Sum(l => l.LineTotal);
            var tolerance = Math.Min(expected * 0.10m, 50m);
            if (invoice.NetAmount > expected + tolerance)
                return $"Invoice net {invoice.NetAmount:N2} exceeds priced work {expected:N2} by more than {tolerance:N2}";
            var expectedVat = contractor.VatRegistered ? Math.Round(invoice.NetAmount * VatRate, 2, MidpointRounding.AwayFromZero) : 0m;
            if (Math.Abs(invoice.VatAmount - expectedVat) > 0.02m)
                return $"VAT {invoice.VatAmount:N2} does not match expected {expectedVat:N2}";
            return null;
        }
    }
}

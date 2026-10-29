using System;
using System.Collections.Generic;
using System.Linq;

namespace Tailspin.DealerOrders.Domain
{
    /// <summary>A dealer quote. Registered in AX as a sales quotation (SalesQuotationTable) once approved.</summary>
    public class Quote
    {
        public Quote()
        {
            Lines = new List<QuoteLine>();
        }

        public int Id { get; set; }
        public string QuoteNumber { get; set; }
        public int DealerId { get; set; }
        public virtual Dealer Dealer { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime ValidUntil { get; set; }
        public QuoteStatus Status { get; set; }
        public string Currency { get; set; }

        /// <summary>End customer the dealer is quoting to; printed on the quote document.</summary>
        public string EndCustomerName { get; set; }
        public string EndCustomerCountry { get; set; }
        public string Reference { get; set; }

        /// <summary>Extra discount (percent of net) the dealer asks for on top of the tier discount.</summary>
        public decimal RequestedExtraDiscount { get; set; }

        public decimal ApprovedExtraDiscount { get; set; }
        public string ApprovalComment { get; set; }
        public string AxQuotationId { get; set; }
        public int? OrderId { get; set; }
        public string Notes { get; set; }
        public byte[] RowVersion { get; set; }

        public virtual ICollection<QuoteLine> Lines { get; set; }

        public decimal TotalNet => Lines.Sum(l => l.NetAmount);
        public decimal TotalList => Lines.Sum(l => l.ListAmount);

        public bool IsExpired(DateTime today) => today.Date > ValidUntil.Date &&
            (Status == QuoteStatus.Draft || Status == QuoteStatus.Submitted || Status == QuoteStatus.Approved || Status == QuoteStatus.RegisteredInAx);

        public bool CanEdit => Status == QuoteStatus.Draft || Status == QuoteStatus.Rejected;

        public bool CanAccept(DateTime today) =>
            (Status == QuoteStatus.Approved || Status == QuoteStatus.RegisteredInAx) && !IsExpired(today);
    }

    public class QuoteLine
    {
        public int Id { get; set; }
        public int QuoteId { get; set; }
        public int LineNumber { get; set; }
        public string ItemId { get; set; }
        public string ModelCode { get; set; }
        public string ConfigurationId { get; set; }

        /// <summary>Selected options, comma separated, as stored with the line.</summary>
        public string OptionCodes { get; set; }

        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal ListPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal NetPrice { get; set; }
        public int LeadTimeWeeks { get; set; }

        public decimal ListAmount => ListPrice * Quantity;
        public decimal NetAmount => NetPrice * Quantity;

        public IEnumerable<string> Options =>
            (OptionCodes ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim());
    }
}

using System;

namespace Tailspin.DealerOrders.Domain
{
    /// <summary>A sales price from an AX trade agreement (PriceDiscTable), cached in SQL by the sync service.</summary>
    public class PriceEntry
    {
        public string ItemId { get; set; }
        public string PriceGroup { get; set; }
        public string Currency { get; set; }
        public decimal Amount { get; set; }
        public decimal QuantityFrom { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public bool IsValidOn(DateTime date) => date.Date >= FromDate.Date && (!ToDate.HasValue || date.Date <= ToDate.Value.Date);
    }

    /// <summary>Dealer discount per discount tier and discount group (line discount agreements in AX).</summary>
    public class DiscountEntry
    {
        public DiscountTier Tier { get; set; }
        public string DiscountGroup { get; set; }
        public decimal Percent { get; set; }
    }

    /// <summary>A campaign that lowers the price of a model or option for a period.</summary>
    public class Campaign
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string ModelCode { get; set; }
        public string OptionCode { get; set; }
        public string CountryCode { get; set; }
        public decimal Percent { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        public bool Applies(string model, string option, string country, DateTime date) =>
            date.Date >= FromDate.Date && date.Date <= ToDate.Date &&
            (string.IsNullOrEmpty(ModelCode) || string.Equals(ModelCode, model, StringComparison.OrdinalIgnoreCase)) &&
            string.Equals(OptionCode ?? "", option ?? "", StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrEmpty(CountryCode) || string.Equals(CountryCode, country, StringComparison.OrdinalIgnoreCase));
    }

    public class ExchangeRate
    {
        public string FromCurrency { get; set; }
        public string ToCurrency { get; set; }

        /// <summary>Units of ToCurrency for one unit of FromCurrency.</summary>
        public decimal Rate { get; set; }

        public DateTime ValidFrom { get; set; }
    }

    /// <summary>On-hand and available quantity per warehouse from AX (InventSum), cached in SQL.</summary>
    public class StockLevel
    {
        public string ItemId { get; set; }
        public string ConfigurationId { get; set; }
        public string Warehouse { get; set; }
        public decimal PhysicalOnHand { get; set; }
        public decimal Reserved { get; set; }
        public decimal OnOrder { get; set; }
        public decimal Available => PhysicalOnHand - Reserved;
        public DateTime? NextReceiptDate { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    /// <summary>Freight charge per destination zone and family for CPT/DAP terms.</summary>
    public class FreightRate
    {
        public string CountryCode { get; set; }
        public string Zone { get; set; }
        public ProductFamily Family { get; set; }
        public decimal AmountEur { get; set; }
    }
}

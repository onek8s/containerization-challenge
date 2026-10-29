using System;
using System.Collections.Generic;

namespace Tailspin.DealerOrders.Contracts.Portal
{
    public class ModelSummaryDto
    {
        public string ModelCode { get; set; }
        public string Name { get; set; }
        public string Family { get; set; }
        public int ModelYear { get; set; }
        public decimal BasePrice { get; set; }
        public string Currency { get; set; }
        public int LeadTimeWeeks { get; set; }
        public string ImageUrl { get; set; }
    }

    public class ConfiguratorModelDto
    {
        public string ModelCode { get; set; }
        public string Name { get; set; }
        public string Currency { get; set; }
        public decimal BasePrice { get; set; }
        public List<OptionGroupDto> Groups { get; set; } = new List<OptionGroupDto>();
        public List<string> DefaultOptions { get; set; } = new List<string>();
    }

    public class OptionGroupDto
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Kind { get; set; }
        public List<OptionDto> Options { get; set; } = new List<OptionDto>();
    }

    public class OptionDto
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public bool IsDefault { get; set; }
    }

    public class ConfigurationRequest
    {
        public string ModelCode { get; set; }
        public List<string> Options { get; set; } = new List<string>();
        public decimal Quantity { get; set; } = 1;
        public decimal ExtraDiscount { get; set; }
        public string DeliveryCountry { get; set; }
    }

    public class ConfigurationResultDto
    {
        public bool IsValid { get; set; }
        public string ConfigurationId { get; set; }
        public List<string> Options { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> AddedByRules { get; set; } = new List<string>();
        public List<PriceLineDto> PriceLines { get; set; } = new List<PriceLineDto>();
        public string Currency { get; set; }
        public decimal ListTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal NetTotal { get; set; }
        public decimal Freight { get; set; }
        public int LeadTimeWeeks { get; set; }
        public DateTime EarliestDelivery { get; set; }
        public bool NeedsApproval { get; set; }
    }

    public class PriceLineDto
    {
        public string Code { get; set; }
        public string Description { get; set; }
        public decimal ListPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal NetPrice { get; set; }
        public string Note { get; set; }
    }

    /// <summary>Message pushed to the dealer portal through SignalR.</summary>
    public class StatusPushDto
    {
        public string Kind { get; set; }
        public int DealerId { get; set; }
        public int? OrderId { get; set; }
        public int? QuoteId { get; set; }
        public string Number { get; set; }
        public string Status { get; set; }
        public string Text { get; set; }
        public DateTime TimestampUtc { get; set; }
    }

    public class StockQueryResultDto
    {
        public string ItemId { get; set; }
        public string Name { get; set; }
        public string ConfigurationId { get; set; }
        public string Warehouse { get; set; }
        public decimal Available { get; set; }
        public decimal OnOrder { get; set; }
        public DateTime? NextReceiptDate { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }
}

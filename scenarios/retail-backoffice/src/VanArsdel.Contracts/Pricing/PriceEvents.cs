using System;
using System.Collections.Generic;

namespace VanArsdel.Contracts.Pricing
{
    public class PriceListPublished
    {
        public int PriceListId { get; set; }
        public string Market { get; set; }
        public string Currency { get; set; }
        public DateTime ValidFrom { get; set; }
        public int ItemCount { get; set; }
        public List<PriceItem> Items { get; set; } = new List<PriceItem>();
    }

    public class PriceItem
    {
        public string Sku { get; set; }
        public decimal Price { get; set; }
        public decimal? CompareAtPrice { get; set; }
    }

    public class PromotionActivated
    {
        public int PromotionId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Markets { get; set; }
        public DateTime StartsUtc { get; set; }
        public DateTime EndsUtc { get; set; }
    }
}

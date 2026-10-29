using System;
using System.Collections.Generic;

namespace VanArsdel.Contracts.Catalog
{
    public class ProductPublished
    {
        public Guid ProductId { get; set; }
        public string StyleCode { get; set; }
        public string Name { get; set; }
        public string Brand { get; set; }
        public string CategoryCode { get; set; }
        public string Season { get; set; }
        public string Gender { get; set; }
        public decimal RecommendedPriceEur { get; set; }
        public decimal RecommendedPriceSek { get; set; }
        public string VatClass { get; set; }
        public List<SkuInfo> Skus { get; set; } = new List<SkuInfo>();
        public DateTime PublishedUtc { get; set; }
    }

    public class ProductUpdated
    {
        public Guid ProductId { get; set; }
        public string StyleCode { get; set; }
        public string Name { get; set; }
        public string CategoryCode { get; set; }
        public List<SkuInfo> Skus { get; set; } = new List<SkuInfo>();
        public DateTime UpdatedUtc { get; set; }
    }

    public class ProductDiscontinued
    {
        public Guid ProductId { get; set; }
        public string StyleCode { get; set; }
        public DateTime EffectiveDate { get; set; }
    }

    public class SkuInfo
    {
        public string Sku { get; set; }
        public string Ean { get; set; }
        public string Colour { get; set; }
        public string ColourCode { get; set; }
        public string Size { get; set; }
        public decimal CostPrice { get; set; }
        public decimal WeightKg { get; set; }
    }
}

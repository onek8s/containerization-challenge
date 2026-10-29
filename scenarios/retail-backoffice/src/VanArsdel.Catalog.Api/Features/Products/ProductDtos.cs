using System;
using System.Collections.Generic;

namespace VanArsdel.Catalog.Api.Features.Products
{
    public class ProductSummaryDto
    {
        public Guid Id { get; set; }
        public string StyleCode { get; set; }
        public string Name { get; set; }
        public string Brand { get; set; }
        public string Category { get; set; }
        public string Season { get; set; }
        public string Status { get; set; }
        public decimal RecommendedPriceEur { get; set; }
        public int SkuCount { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    public class ProductDto
    {
        public Guid Id { get; set; }
        public string StyleCode { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string BrandCode { get; set; }
        public string BrandName { get; set; }
        public string CategoryCode { get; set; }
        public string CategoryName { get; set; }
        public string Season { get; set; }
        public string Gender { get; set; }
        public string Material { get; set; }
        public string CareInstructions { get; set; }
        public string VatClass { get; set; }
        public decimal RecommendedPriceEur { get; set; }
        public decimal RecommendedPriceSek { get; set; }
        public string Status { get; set; }
        public DateTime? PublishedUtc { get; set; }
        public DateTime? DiscontinuedOn { get; set; }
        public string RowVersion { get; set; }
        public List<SkuDto> Skus { get; set; } = new List<SkuDto>();
        public List<string> Images { get; set; } = new List<string>();
    }

    public class SkuDto
    {
        public string SkuCode { get; set; }
        public string Ean { get; set; }
        public string ColourCode { get; set; }
        public string ColourName { get; set; }
        public string Size { get; set; }
        public decimal CostPrice { get; set; }
        public decimal WeightKg { get; set; }
        public bool Blocked { get; set; }
    }

    public class SkuLookupDto
    {
        public string SkuCode { get; set; }
        public string Ean { get; set; }
        public string StyleCode { get; set; }
        public string Name { get; set; }
        public string ColourName { get; set; }
        public string Size { get; set; }
        public decimal WeightKg { get; set; }
        public decimal RecommendedPriceEur { get; set; }
    }

    public class PagedResult<T>
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public List<T> Items { get; set; } = new List<T>();
    }
}

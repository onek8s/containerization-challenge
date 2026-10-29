using System;
using System.Collections.Generic;
using System.Linq;
using VanArsdel.Catalog.Api.Domain;

namespace VanArsdel.Catalog.Api.Search
{
    public class ProductDocument
    {
        public string Id { get; set; }
        public string StyleCode { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Brand { get; set; }
        public string Category { get; set; }
        public string CategoryPath { get; set; }
        public string Season { get; set; }
        public string Gender { get; set; }
        public string Status { get; set; }
        public decimal PriceEur { get; set; }
        public List<string> Colours { get; set; } = new List<string>();
        public List<string> Sizes { get; set; } = new List<string>();
        public List<string> Skus { get; set; } = new List<string>();
        public List<string> Eans { get; set; } = new List<string>();
        public DateTime UpdatedUtc { get; set; }

        public static ProductDocument From(Product p) => new ProductDocument
        {
            Id = p.Id.ToString(),
            StyleCode = p.StyleCode,
            Name = p.Name,
            Description = p.Description,
            Brand = p.Brand?.Name,
            Category = p.Category?.Code,
            CategoryPath = p.Category?.Parent != null ? p.Category.Parent.Name + " / " + p.Category.Name : p.Category?.Name,
            Season = p.Season,
            Gender = p.Gender,
            Status = p.Status.ToString(),
            PriceEur = p.RecommendedPriceEur,
            Colours = p.Skus.Select(s => s.ColourName).Where(c => c != null).Distinct().ToList(),
            Sizes = p.Skus.OrderBy(s => s.SortOrder).Select(s => s.Size).Distinct().ToList(),
            Skus = p.Skus.Select(s => s.SkuCode).ToList(),
            Eans = p.Skus.Select(s => s.Ean).Where(e => e != null).ToList(),
            UpdatedUtc = p.UpdatedUtc,
        };
    }

    public class ProductSearchQuery
    {
        public string Text { get; set; }
        public string Category { get; set; }
        public string Brand { get; set; }
        public string Season { get; set; }
        public string Colour { get; set; }
        public string Size { get; set; }
        public bool IncludeDrafts { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 24;
    }

    public class ProductSearchResult
    {
        public long Total { get; set; }
        public List<ProductDocument> Items { get; set; } = new List<ProductDocument>();
        public Dictionary<string, Dictionary<string, long>> Facets { get; set; } = new Dictionary<string, Dictionary<string, long>>();
        public string Engine { get; set; }
    }
}

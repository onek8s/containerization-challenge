using System;
using System.Collections.Generic;
using System.Linq;
using VanArsdel.BuildingBlocks.Exceptions;

namespace VanArsdel.Catalog.Api.Domain
{
    public enum ProductStatus
    {
        Draft = 0,
        Published = 1,
        Discontinued = 2,
    }

    public class Product
    {
        public Guid Id { get; set; }
        public string StyleCode { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int BrandId { get; set; }
        public Brand Brand { get; set; }
        public int CategoryId { get; set; }
        public Category Category { get; set; }
        public string Season { get; set; }
        public string Gender { get; set; }
        public string Material { get; set; }
        public string CareInstructions { get; set; }
        public string VatClass { get; set; } = "Standard";
        public decimal RecommendedPriceEur { get; set; }
        public decimal RecommendedPriceSek { get; set; }
        public ProductStatus Status { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public DateTime? PublishedUtc { get; set; }
        public DateTime? DiscontinuedOn { get; set; }
        public byte[] RowVersion { get; set; }
        public List<Sku> Skus { get; set; } = new List<Sku>();
        public List<ProductImage> Images { get; set; } = new List<ProductImage>();

        public void Publish(DateTime now)
        {
            if (Status == ProductStatus.Discontinued) throw new DomainException($"{StyleCode} is discontinued.");
            if (Skus.Count == 0) throw new DomainException($"{StyleCode} has no SKUs.");
            if (RecommendedPriceEur <= 0) throw new DomainException($"{StyleCode} has no recommended price.");
            var missingEan = Skus.Where(s => string.IsNullOrEmpty(s.Ean)).Select(s => s.SkuCode).ToList();
            if (missingEan.Count > 0) throw new DomainException("SKUs without EAN: " + string.Join(", ", missingEan));

            Status = ProductStatus.Published;
            PublishedUtc ??= now;
            UpdatedUtc = now;
        }

        public void Discontinue(DateTime effective, DateTime now)
        {
            if (Status == ProductStatus.Discontinued) return;
            Status = ProductStatus.Discontinued;
            DiscontinuedOn = effective.Date;
            UpdatedUtc = now;
        }

        public Sku AddSku(string colourCode, string colourName, string size, string ean, decimal costPrice, decimal weightKg, int sortOrder)
        {
            var code = SkuCodeFor(StyleCode, colourCode, size);
            if (Skus.Any(s => s.SkuCode == code)) throw new ConflictException($"SKU {code} already exists.");
            if (!string.IsNullOrEmpty(ean) && !Ean13.IsValid(ean)) throw new DomainException($"EAN {ean} is not a valid EAN-13.");
            var sku = new Sku
            {
                SkuCode = code,
                ColourCode = colourCode,
                ColourName = colourName,
                Size = size,
                Ean = ean,
                CostPrice = costPrice,
                WeightKg = weightKg,
                SortOrder = sortOrder,
            };
            Skus.Add(sku);
            return sku;
        }

        public static string SkuCodeFor(string styleCode, string colourCode, string size) =>
            $"{styleCode}-{colourCode}-{size}".ToUpperInvariant().Replace(" ", string.Empty);
    }

    public class Sku
    {
        public int Id { get; set; }
        public Guid ProductId { get; set; }
        public string SkuCode { get; set; }
        public string Ean { get; set; }
        public string ColourCode { get; set; }
        public string ColourName { get; set; }
        public string Size { get; set; }
        public int SortOrder { get; set; }
        public decimal CostPrice { get; set; }
        public decimal WeightKg { get; set; }
        public bool Blocked { get; set; }
    }

    public class ProductImage
    {
        public int Id { get; set; }
        public Guid ProductId { get; set; }
        public string ColourCode { get; set; }
        public string Url { get; set; }
        public int SortOrder { get; set; }
    }

    public class Brand
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public bool OwnBrand { get; set; }
    }

    public class Category
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public int? ParentId { get; set; }
        public Category Parent { get; set; }
        public string ErpItemGroup { get; set; }
        public string SizeScale { get; set; }
    }

    public static class Ean13
    {
        public static bool IsValid(string ean)
        {
            if (ean == null || ean.Length != 13 || !ean.All(char.IsDigit)) return false;
            return ean[12] - '0' == CheckDigit(ean.Substring(0, 12));
        }

        public static int CheckDigit(string first12)
        {
            var sum = 0;
            for (var i = 0; i < 12; i++)
            {
                var d = first12[i] - '0';
                sum += i % 2 == 0 ? d : d * 3;
            }
            return (10 - sum % 10) % 10;
        }

        /// <summary>Company prefix 6438521 (GS1 Finland) + 5-digit item reference.</summary>
        public static string FromSequence(int sequence)
        {
            var first12 = "6438521" + (sequence % 100000).ToString("00000");
            return first12 + CheckDigit(first12);
        }
    }
}

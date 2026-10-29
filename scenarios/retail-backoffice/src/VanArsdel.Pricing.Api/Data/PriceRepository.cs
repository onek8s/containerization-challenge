using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using VanArsdel.Pricing.Api.Domain;

namespace VanArsdel.Pricing.Api.Data
{
    public class SkuPrice
    {
        public string Sku { get; set; }
        public string Market { get; set; }
        public string Currency { get; set; }
        public decimal Price { get; set; }
        public decimal? CompareAtPrice { get; set; }
        public int? PriceListId { get; set; }
    }

    public class SkuAttribute
    {
        public string Sku { get; set; }
        public string StyleCode { get; set; }
        public string CategoryCode { get; set; }
        public string Brand { get; set; }
        public string Season { get; set; }
        public bool Discontinued { get; set; }
    }

    public class PriceRepository
    {
        private readonly IDbConnectionFactory _db;

        public PriceRepository(IDbConnectionFactory db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<SkuPrice>> GetPricesAsync(string market, IEnumerable<string> skus)
        {
            var list = skus.Distinct().ToList();
            if (list.Count == 0) return Array.Empty<SkuPrice>();
            using var c = _db.Open();
            var rows = await c.QueryAsync<SkuPrice>(@"
SELECT b.Sku, b.Market, b.Currency,
       COALESCE(e.Price, b.Price) AS Price,
       e.CompareAtPrice,
       e.PriceListId
FROM BasePrice b
LEFT JOIN EffectivePrice e ON e.Sku = b.Sku AND e.Market = b.Market AND e.ValidFrom <= @Today
WHERE b.Market = @Market AND b.Sku IN @Skus", new { Market = market, Skus = list, Today = DateTime.UtcNow.Date });
            return rows.ToList();
        }

        public async Task<IReadOnlyList<SkuAttribute>> GetAttributesAsync(IEnumerable<string> skus)
        {
            var list = skus.Distinct().ToList();
            if (list.Count == 0) return Array.Empty<SkuAttribute>();
            using var c = _db.Open();
            return (await c.QueryAsync<SkuAttribute>("SELECT Sku, StyleCode, CategoryCode, Brand, Season, Discontinued FROM SkuAttribute WHERE Sku IN @Skus", new { Skus = list })).ToList();
        }

        public async Task UpsertSkuAsync(SkuAttribute attribute, IEnumerable<(string Market, string Currency, decimal Price)> basePrices)
        {
            using var c = _db.Open();
            using var tx = c.BeginTransaction();
            await c.ExecuteAsync(_db.Upsert("SkuAttribute", new[] { "Sku" }, new[] { "StyleCode", "CategoryCode", "Brand", "Season", "Discontinued", "UpdatedUtc" }),
                new { attribute.Sku, attribute.StyleCode, attribute.CategoryCode, attribute.Brand, attribute.Season, attribute.Discontinued, UpdatedUtc = DateTime.UtcNow }, tx);
            foreach (var p in basePrices)
            {
                await c.ExecuteAsync(_db.Upsert("BasePrice", new[] { "Sku", "Market" }, new[] { "Currency", "Price", "UpdatedUtc" }),
                    new { attribute.Sku, p.Market, p.Currency, p.Price, UpdatedUtc = DateTime.UtcNow }, tx);
            }
            tx.Commit();
        }

        public async Task MarkDiscontinuedAsync(string styleCode)
        {
            using var c = _db.Open();
            await c.ExecuteAsync("UPDATE SkuAttribute SET Discontinued = 1, UpdatedUtc = @Now WHERE StyleCode = @StyleCode", new { StyleCode = styleCode, Now = DateTime.UtcNow });
        }

        public async Task<int> CountSkusAsync()
        {
            using var c = _db.Open();
            return await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM SkuAttribute");
        }
    }
}

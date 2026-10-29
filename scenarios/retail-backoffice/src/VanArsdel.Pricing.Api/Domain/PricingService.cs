using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using VanArsdel.BuildingBlocks.Caching;
using VanArsdel.BuildingBlocks.Exceptions;
using VanArsdel.Contracts;
using VanArsdel.Pricing.Api.Data;

namespace VanArsdel.Pricing.Api.Domain
{
    public class QuoteRequest
    {
        public string Market { get; set; } = "FI";
        public string Channel { get; set; } = "Web";
        public string PromotionCode { get; set; }
        public List<QuoteLine> Lines { get; set; } = new List<QuoteLine>();
    }

    public class QuoteLine
    {
        public string Sku { get; set; }
        public int Quantity { get; set; }
    }

    public class PricingService
    {
        private readonly PriceRepository _prices;
        private readonly PromotionRepository _promotions;
        private readonly IDistributedCache _cache;
        private readonly PromotionEngine _engine = new PromotionEngine();

        public PricingService(PriceRepository prices, PromotionRepository promotions, IDistributedCache cache)
        {
            _prices = prices;
            _promotions = promotions;
            _cache = cache;
        }

        public async Task<IReadOnlyList<SkuPrice>> GetPricesAsync(string market, IReadOnlyCollection<string> skus, CancellationToken ct)
        {
            market = NormalizeMarket(market);
            var result = new List<SkuPrice>();
            var missing = new List<string>();
            foreach (var sku in skus.Distinct())
            {
                var cached = await _cache.GetStringAsync(Key(market, sku), ct);
                if (cached != null) result.Add(Newtonsoft.Json.JsonConvert.DeserializeObject<SkuPrice>(cached));
                else missing.Add(sku);
            }

            if (missing.Count > 0)
            {
                foreach (var batch in missing.Select((s, i) => new { s, i }).GroupBy(x => x.i / 500, x => x.s))
                {
                    var rows = await _prices.GetPricesAsync(market, batch);
                    foreach (var row in rows)
                    {
                        await _cache.SetStringAsync(Key(market, row.Sku), Newtonsoft.Json.JsonConvert.SerializeObject(row),
                            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) }, ct);
                        result.Add(row);
                    }
                }
            }
            return result;
        }

        public async Task<BasketResult> QuoteAsync(QuoteRequest request, CancellationToken ct)
        {
            var market = NormalizeMarket(request.Market);
            var skus = request.Lines.Where(l => l.Quantity > 0).Select(l => l.Sku).Distinct().ToList();
            var prices = (await GetPricesAsync(market, skus, ct)).ToDictionary(p => p.Sku, StringComparer.OrdinalIgnoreCase);
            var attributes = (await _prices.GetAttributesAsync(skus)).ToDictionary(a => a.Sku, StringComparer.OrdinalIgnoreCase);

            var unknown = skus.Where(s => !prices.ContainsKey(s)).ToList();
            if (unknown.Count > 0) throw new DomainException("No price for: " + string.Join(", ", unknown));

            var lines = request.Lines.Where(l => l.Quantity > 0).GroupBy(l => l.Sku, StringComparer.OrdinalIgnoreCase).Select(g =>
            {
                attributes.TryGetValue(g.Key, out var a);
                var p = prices[g.Key];
                return new BasketLine
                {
                    Sku = p.Sku,
                    Quantity = g.Sum(x => x.Quantity),
                    UnitPrice = p.Price,
                    CompareAtPrice = p.CompareAtPrice,
                    StyleCode = a?.StyleCode,
                    CategoryCode = a?.CategoryCode,
                    Brand = a?.Brand,
                    Season = a?.Season,
                };
            }).ToList();

            var now = DateTime.UtcNow;
            var active = await _cache.GetOrSetJsonAsync("promotions:active", TimeSpan.FromMinutes(2),
                async () => (await _promotions.ActiveAsync(now)).ToList(), ct);
            var vat = Markets.StandardVat.TryGetValue(market, out var v) ? v : 0.24m;
            return _engine.Apply(market, Markets.CurrencyOf(market), vat, lines, active, now, request.Channel, request.PromotionCode);
        }

        public async Task InvalidateAsync(string market, IEnumerable<string> skus, CancellationToken ct)
        {
            foreach (var sku in skus) await _cache.RemoveAsync(Key(market, sku), ct);
        }

        public Task InvalidatePromotionsAsync(CancellationToken ct) => _cache.RemoveAsync("promotions:active", ct);

        private static string NormalizeMarket(string market)
        {
            var m = (market ?? "FI").ToUpperInvariant();
            if (!Markets.Currencies.ContainsKey(m)) throw new DomainException($"Unknown market {market}.");
            return m;
        }

        private static string Key(string market, string sku) => $"price:{market}:{sku.ToUpperInvariant()}";
    }
}

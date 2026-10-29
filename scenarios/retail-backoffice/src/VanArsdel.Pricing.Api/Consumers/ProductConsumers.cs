using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using VanArsdel.Contracts;
using VanArsdel.Contracts.Catalog;
using VanArsdel.Pricing.Api.Data;
using VanArsdel.Pricing.Api.Domain;

namespace VanArsdel.Pricing.Api.Consumers
{
    /// <summary>New styles get their recommended retail price as base price in every market.</summary>
    public class ProductPublishedConsumer : IConsumer<ProductPublished>, IConsumer<ProductUpdated>
    {
        private readonly PriceRepository _prices;
        private readonly PricingService _pricing;
        private readonly ILogger<ProductPublishedConsumer> _logger;

        public ProductPublishedConsumer(PriceRepository prices, PricingService pricing, ILogger<ProductPublishedConsumer> logger)
        {
            _prices = prices;
            _pricing = pricing;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<ProductPublished> context)
        {
            var m = context.Message;
            var basePrices = Markets.Currencies.Select(kv => (kv.Key, kv.Value, kv.Value == "SEK" ? m.RecommendedPriceSek : m.RecommendedPriceEur)).ToList();
            foreach (var sku in m.Skus)
            {
                await _prices.UpsertSkuAsync(new SkuAttribute
                {
                    Sku = sku.Sku,
                    StyleCode = m.StyleCode,
                    CategoryCode = m.CategoryCode,
                    Brand = m.Brand,
                    Season = m.Season,
                }, basePrices);
                foreach (var market in Markets.Currencies.Keys)
                {
                    await _pricing.InvalidateAsync(market, new[] { sku.Sku }, context.CancellationToken);
                }
            }
            _logger.LogInformation("Base prices set for {StyleCode} ({Count} SKUs)", m.StyleCode, m.Skus.Count);
        }

        public async Task Consume(ConsumeContext<ProductUpdated> context)
        {
            var m = context.Message;
            var existing = (await _prices.GetAttributesAsync(m.Skus.Select(s => s.Sku))).ToDictionary(a => a.Sku);
            foreach (var sku in m.Skus.Where(s => existing.ContainsKey(s.Sku)))
            {
                var a = existing[sku.Sku];
                a.StyleCode = m.StyleCode;
                a.CategoryCode = m.CategoryCode;
                await _prices.UpsertSkuAsync(a, Enumerable.Empty<(string, string, decimal)>());
            }
        }
    }

    public class ProductDiscontinuedConsumer : IConsumer<ProductDiscontinued>
    {
        private readonly PriceRepository _prices;

        public ProductDiscontinuedConsumer(PriceRepository prices)
        {
            _prices = prices;
        }

        public Task Consume(ConsumeContext<ProductDiscontinued> context) => _prices.MarkDiscontinuedAsync(context.Message.StyleCode);
    }
}

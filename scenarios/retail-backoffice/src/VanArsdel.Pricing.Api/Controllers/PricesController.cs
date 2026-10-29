using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanArsdel.BuildingBlocks.Exceptions;
using VanArsdel.Pricing.Api.Data;
using VanArsdel.Pricing.Api.Domain;

namespace VanArsdel.Pricing.Api.Controllers
{
    [ApiController]
    [Route("api/prices")]
    [Authorize(Policy = Policies.Read)]
    public class PricesController : ControllerBase
    {
        private readonly PricingService _pricing;

        public PricesController(PricingService pricing)
        {
            _pricing = pricing;
        }

        /// <summary>Current selling price of one SKU in a market.</summary>
        [HttpGet("{sku}")]
        public async Task<SkuPrice> Get(string sku, [FromQuery] string market, CancellationToken ct)
        {
            var prices = await _pricing.GetPricesAsync(market ?? "FI", new[] { sku }, ct);
            return prices.FirstOrDefault() ?? throw new NotFoundException("Price", sku);
        }

        [HttpPost("lookup")]
        public Task<IReadOnlyList<SkuPrice>> Lookup([FromBody] PriceLookupRequest request, CancellationToken ct) =>
            _pricing.GetPricesAsync(request.Market, request.Skus, ct);

        /// <summary>Prices a basket with the active promotions (web shop checkout, store endless aisle, orders).</summary>
        [HttpPost("quote")]
        public Task<BasketResult> Quote([FromBody] QuoteRequest request, CancellationToken ct) => _pricing.QuoteAsync(request, ct);
    }

    public class PriceLookupRequest
    {
        public string Market { get; set; } = "FI";
        public List<string> Skus { get; set; } = new List<string>();
    }
}

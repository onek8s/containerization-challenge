using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using VanArsdel.BuildingBlocks.Exceptions;
using VanArsdel.BuildingBlocks.Security;
using VanArsdel.Contracts;
using VanArsdel.Contracts.Pricing;
using VanArsdel.Pricing.Api.Data;
using VanArsdel.Pricing.Api.Domain;

namespace VanArsdel.Pricing.Api.Controllers
{
    [ApiController]
    [Route("api/pricelists")]
    [Authorize(Policy = Policies.Read)]
    public class PriceListsController : ControllerBase
    {
        private readonly PriceListRepository _lists;
        private readonly PricingService _pricing;
        private readonly IPublishEndpoint _publish;
        private readonly ILogger<PriceListsController> _logger;

        public PriceListsController(PriceListRepository lists, PricingService pricing, IPublishEndpoint publish, ILogger<PriceListsController> logger)
        {
            _lists = lists;
            _pricing = pricing;
            _publish = publish;
            _logger = logger;
        }

        [HttpGet]
        public Task<IReadOnlyList<PriceList>> List([FromQuery] string market, [FromQuery] string status) => _lists.ListAsync(market, status);

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var list = await _lists.GetAsync(id) ?? throw new NotFoundException("PriceList", id);
            return Ok(new { list, items = await _lists.GetItemsAsync(id) });
        }

        [HttpPost]
        [Authorize(Policy = Policies.Write)]
        public async Task<IActionResult> Create([FromBody] CreatePriceList request)
        {
            var id = await _lists.CreateAsync(new PriceList
            {
                Name = request.Name,
                Market = request.Market.ToUpperInvariant(),
                Currency = Markets.CurrencyOf(request.Market),
                ValidFrom = request.ValidFrom.Date,
                Status = PriceListStatus.Draft,
                CreatedBy = User.UserName(),
                CreatedUtc = DateTime.UtcNow,
            }, request.Items);
            return CreatedAtAction(nameof(Get), new { id }, new { id });
        }

        /// <summary>Marks a draft list ready; the scheduler publishes it on its valid-from date.</summary>
        [HttpPost("{id:int}/schedule")]
        [Authorize(Policy = Policies.Write)]
        public async Task<IActionResult> Schedule(int id)
        {
            if (!await _lists.SetStatusAsync(id, PriceListStatus.Draft, PriceListStatus.Scheduled))
            {
                throw new ConflictException("Only draft price lists can be scheduled.");
            }
            return NoContent();
        }

        [HttpPost("{id:int}/cancel")]
        [Authorize(Policy = Policies.Write)]
        public async Task<IActionResult> Cancel(int id)
        {
            if (!await _lists.SetStatusAsync(id, PriceListStatus.Scheduled, PriceListStatus.Cancelled)
                && !await _lists.SetStatusAsync(id, PriceListStatus.Draft, PriceListStatus.Cancelled))
            {
                throw new ConflictException("Published price lists cannot be cancelled.");
            }
            return NoContent();
        }

        /// <summary>
        /// Publishes scheduled lists valid on or before the given date (default: tomorrow, so that the
        /// stores receive the prices in the nightly POS download). Called by the scheduler at 02:00.
        /// </summary>
        [HttpPost("publish-due")]
        [Authorize(Policy = Policies.Write)]
        public async Task<IActionResult> PublishDue([FromQuery] DateTime? upTo, CancellationToken ct)
        {
            var due = await _lists.DueAsync(upTo ?? DateTime.UtcNow.Date.AddDays(1));
            var published = new List<object>();
            foreach (var list in due)
            {
                var items = await _lists.PublishAsync(list.Id);
                await _pricing.InvalidateAsync(list.Market, items.Select(i => i.Sku), ct);

                // Published straight to the bus; this service has no outbox table (Dapper, no EF).
                await _publish.Publish(new PriceListPublished
                {
                    PriceListId = list.Id,
                    Market = list.Market,
                    Currency = list.Currency,
                    ValidFrom = list.ValidFrom,
                    ItemCount = items.Count,
                    Items = items.Select(i => new PriceItem { Sku = i.Sku, Price = i.Price, CompareAtPrice = i.CompareAtPrice }).ToList(),
                }, ct);
                _logger.LogInformation("Published price list {PriceListId} {Market} valid from {ValidFrom:yyyy-MM-dd} with {Count} items", list.Id, list.Market, list.ValidFrom, items.Count);
                published.Add(new { list.Id, list.Market, items = items.Count });
            }
            return Ok(new { published });
        }
    }

    public class CreatePriceList
    {
        public string Name { get; set; }
        public string Market { get; set; }
        public DateTime ValidFrom { get; set; }
        public List<PriceListItem> Items { get; set; } = new List<PriceListItem>();
    }

    public class CreatePriceListValidator : AbstractValidator<CreatePriceList>
    {
        public CreatePriceListValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Market).NotEmpty().Must(m => m != null && Markets.Currencies.ContainsKey(m.ToUpperInvariant())).WithMessage("Unknown market.");
            RuleFor(x => x.ValidFrom).GreaterThanOrEqualTo(DateTime.UtcNow.Date.AddDays(-1));
            RuleFor(x => x.Items).NotEmpty();
            RuleForEach(x => x.Items).ChildRules(i =>
            {
                i.RuleFor(x => x.Sku).NotEmpty();
                i.RuleFor(x => x.Price).GreaterThan(0);
                i.RuleFor(x => x.CompareAtPrice).GreaterThan(x => x.Price).When(x => x.CompareAtPrice.HasValue);
            });
        }
    }
}

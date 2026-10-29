using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanArsdel.BuildingBlocks.Exceptions;
using VanArsdel.Contracts.Pricing;
using VanArsdel.Pricing.Api.Data;
using VanArsdel.Pricing.Api.Domain;

namespace VanArsdel.Pricing.Api.Controllers
{
    [ApiController]
    [Route("api/promotions")]
    [Authorize(Policy = Policies.Read)]
    public class PromotionsController : ControllerBase
    {
        private readonly PromotionRepository _promotions;
        private readonly PricingService _pricing;
        private readonly IPublishEndpoint _publish;

        public PromotionsController(PromotionRepository promotions, PricingService pricing, IPublishEndpoint publish)
        {
            _promotions = promotions;
            _pricing = pricing;
            _publish = publish;
        }

        [HttpGet]
        public Task<IReadOnlyList<Promotion>> List([FromQuery] bool currentOnly = false) => _promotions.ListAsync(currentOnly);

        [HttpGet("{code}")]
        public async Task<Promotion> Get(string code) => await _promotions.GetAsync(code) ?? throw new NotFoundException("Promotion", code);

        [HttpPut("{code}")]
        [Authorize(Policy = Policies.Write)]
        public async Task<Promotion> Save(string code, [FromBody] Promotion promotion, CancellationToken ct)
        {
            promotion.Code = code.ToUpperInvariant();
            var existing = await _promotions.GetAsync(promotion.Code);
            promotion.Status = existing?.Status ?? "Draft";
            promotion.CreatedUtc = existing?.CreatedUtc ?? DateTime.UtcNow;
            await _promotions.SaveAsync(promotion);
            await _pricing.InvalidatePromotionsAsync(ct);
            return await _promotions.GetAsync(promotion.Code);
        }

        [HttpPost("{code}/activate")]
        [Authorize(Policy = Policies.Write)]
        public async Task<IActionResult> Activate(string code, CancellationToken ct)
        {
            var p = await _promotions.GetAsync(code) ?? throw new NotFoundException("Promotion", code);
            if (p.EndsUtc <= DateTime.UtcNow) throw new DomainException("The promotion has already ended.");
            await _promotions.SetStatusAsync(p.Code, "Active");
            await _pricing.InvalidatePromotionsAsync(ct);
            await _publish.Publish(new PromotionActivated { PromotionId = p.Id, Code = p.Code, Name = p.Name, Markets = p.Markets, StartsUtc = p.StartsUtc, EndsUtc = p.EndsUtc }, ct);
            return NoContent();
        }

        [HttpPost("{code}/deactivate")]
        [Authorize(Policy = Policies.Write)]
        public async Task<IActionResult> Deactivate(string code, CancellationToken ct)
        {
            if (!await _promotions.SetStatusAsync(code.ToUpperInvariant(), "Inactive")) throw new NotFoundException("Promotion", code);
            await _pricing.InvalidatePromotionsAsync(ct);
            return NoContent();
        }
    }

    public class PromotionValidator : AbstractValidator<Promotion>
    {
        public PromotionValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Type).Must(t => PromotionTypes.All.Contains(t)).WithMessage("Type is one of " + string.Join(", ", PromotionTypes.All) + ".");
            RuleFor(x => x.Channel).Must(c => c == "All" || c == "Store" || c == "Web");
            RuleFor(x => x.Markets).NotEmpty();
            RuleFor(x => x.EndsUtc).GreaterThan(x => x.StartsUtc);
            RuleFor(x => x.Value).InclusiveBetween(0, 100).When(x => x.Type == PromotionTypes.PercentOff || x.Type == PromotionTypes.Markdown);
            RuleFor(x => x.BuyQuantity).NotNull().GreaterThan(1).When(x => x.Type == PromotionTypes.BuyXPayY || x.Type == PromotionTypes.Bundle);
            RuleFor(x => x.PayQuantity).NotNull().LessThan(x => x.BuyQuantity).When(x => x.Type == PromotionTypes.BuyXPayY);
            RuleForEach(x => x.Scopes).ChildRules(s =>
                s.RuleFor(x => x.ScopeType).Must(t => new[] { "Sku", "Style", "Category", "Brand", "Season" }.Contains(t)));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanArsdel.Catalog.Api.Features.Products;

namespace VanArsdel.Catalog.Api.Controllers
{
    [ApiController]
    [Route("api/products")]
    [Authorize(Policy = Policies.Read)]
    public class ProductsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ProductsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>Lists products for the back office.</summary>
        [HttpGet]
        public Task<PagedResult<ProductSummaryDto>> List([FromQuery] GetProducts query) => _mediator.Send(query);

        /// <summary>Gets one product by style code or id.</summary>
        [HttpGet("{styleCodeOrId}")]
        public Task<ProductDto> Get(string styleCodeOrId) => _mediator.Send(new GetProduct { StyleCodeOrId = styleCodeOrId });

        /// <summary>Published product for the web shop product page (aggregated with availability by the gateway).</summary>
        [HttpGet("{styleCode}/public")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> GetPublic(string styleCode)
        {
            var p = await _mediator.Send(new GetProduct { StyleCodeOrId = styleCode });
            if (p.Status != "Published")
            {
                return NotFound();
            }
            // Cost prices, row versions and internal status stay inside.
            return new
            {
                p.StyleCode, p.Name, p.Description, p.BrandName, p.CategoryCode, p.CategoryName, p.Gender, p.Material, p.CareInstructions,
                p.RecommendedPriceEur, p.RecommendedPriceSek, p.Images,
                Skus = p.Skus.Select(s => new { s.SkuCode, s.Ean, s.ColourCode, s.ColourName, s.Size }),
            };
        }

        /// <summary>Resolves SKU codes or EANs (scanner input in the stores).</summary>
        [HttpPost("skus/lookup")]
        public Task<List<SkuLookupDto>> LookupSkus([FromBody] List<string> codes) => _mediator.Send(new LookupSkus { Codes = codes });

        [HttpPost]
        [Authorize(Policy = Policies.Write)]
        public async Task<ActionResult<ProductDto>> Create([FromBody] SaveProduct command)
        {
            command.Id = null;
            var product = await _mediator.Send(command);
            return CreatedAtAction(nameof(Get), new { styleCodeOrId = product.StyleCode }, product);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = Policies.Write)]
        public Task<ProductDto> Update(Guid id, [FromBody] SaveProduct command)
        {
            command.Id = id;
            return _mediator.Send(command);
        }

        [HttpPost("{styleCode}/publish")]
        [Authorize(Policy = Policies.Write)]
        public Task<ProductDto> Publish(string styleCode) => _mediator.Send(new PublishProduct { StyleCode = styleCode });

        [HttpPost("{styleCode}/discontinue")]
        [Authorize(Policy = Policies.Write)]
        public Task<ProductDto> Discontinue(string styleCode, [FromQuery] DateTime? effectiveDate) =>
            _mediator.Send(new DiscontinueProduct { StyleCode = styleCode, EffectiveDate = effectiveDate ?? DateTime.UtcNow.Date });
    }
}

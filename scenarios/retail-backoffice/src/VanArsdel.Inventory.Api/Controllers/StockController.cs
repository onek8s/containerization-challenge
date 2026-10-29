using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanArsdel.BuildingBlocks.Security;
using VanArsdel.Inventory.Api.Data;
using VanArsdel.Inventory.Api.Domain;
using VanArsdel.Inventory.Api.Services;

namespace VanArsdel.Inventory.Api.Controllers
{
    [ApiController]
    [Route("api")]
    [Authorize(Policy = Policies.Read)]
    public class StockController : ControllerBase
    {
        private readonly InventoryDbContext _db;
        private readonly StockService _stock;
        private readonly AvailabilityQueries _availability;

        public StockController(InventoryDbContext db, StockService stock, AvailabilityQueries availability)
        {
            _db = db;
            _stock = stock;
            _availability = availability;
        }

        [HttpGet("locations")]
        public async Task<IActionResult> Locations() =>
            Ok(await _db.Locations.AsNoTracking().OrderBy(l => l.Code).ToListAsync());

        /// <summary>Stock of one SKU in all locations (store staff "find in other stores").</summary>
        [HttpGet("stock/sku/{sku}")]
        public async Task<IActionResult> BySku(string sku, CancellationToken ct)
        {
            var rows = await (from s in _db.StockLevels.AsNoTracking()
                              join l in _db.Locations.AsNoTracking() on s.LocationCode equals l.Code
                              where s.Sku == sku.ToUpper()
                              orderby l.Code
                              select new { l.Code, l.Name, l.City, l.Type, s.OnHand, s.Reserved, s.InTransit, Available = s.OnHand - s.Reserved }).ToListAsync(ct);
            return Ok(rows);
        }

        [HttpGet("stock/{locationCode}")]
        public async Task<IActionResult> ByLocation(string locationCode, [FromQuery] string style, [FromQuery] bool belowMinOnly, [FromQuery] int take = 500)
        {
            var q = _db.StockLevels.AsNoTracking().Where(s => s.LocationCode == locationCode);
            if (!string.IsNullOrEmpty(style)) q = q.Where(s => s.Sku.StartsWith(style.ToUpper() + "-"));
            if (belowMinOnly) q = q.Where(s => s.MinLevel > 0 && s.OnHand + s.InTransit < s.MinLevel);
            return Ok(await q.OrderBy(s => s.Sku).Take(Math.Clamp(take, 1, 5000))
                .Select(s => new { s.Sku, s.OnHand, s.Reserved, s.InTransit, s.MinLevel, s.MaxLevel, Available = s.OnHand - s.Reserved, s.UpdatedUtc })
                .ToListAsync());
        }

        [HttpPost("stock/availability")]
        public async Task<IActionResult> Availability([FromBody] AvailabilityQuery query, CancellationToken ct)
        {
            var result = await _availability.GetAsync(query.Skus, query.Locations ?? new List<string>(), query.Country, false, ct);
            return Ok(result.Where(r => r.Levels.Count > 0).Select(r => new
            {
                r.Location.Code,
                r.Location.Name,
                r.Location.Type,
                Items = r.Levels.Select(l => new { l.Sku, l.Available, l.OnHand }),
            }));
        }

        /// <summary>Coarse availability per size for the web shop product page.</summary>
        [HttpGet("availability/styles/{styleCode}/summary")]
        [AllowAnonymous]
        public Task<StyleAvailabilitySummary> StyleSummary(string styleCode, CancellationToken ct) => _availability.StyleSummaryAsync(styleCode.ToUpperInvariant(), ct);

        /// <summary>Manual adjustment by store staff (damaged, found, theft).</summary>
        [HttpPost("stock/{locationCode}/adjustments")]
        [Authorize(Policy = Policies.Adjust)]
        public async Task<IActionResult> Adjust(string locationCode, [FromBody] AdjustmentRequest request, CancellationToken ct)
        {
            if (!User.HasAnyRole(VanArsdel.Contracts.Roles.Merchandiser, VanArsdel.Contracts.Roles.Admin)
                && !string.Equals(User.StoreCode(), locationCode, StringComparison.OrdinalIgnoreCase)
                && !User.HasAnyRole(VanArsdel.Contracts.Roles.Service))
            {
                return Forbid();
            }
            await _stock.ApplyAsync(locationCode, request.Lines.Select(l => new StockDelta(l.Sku, l.Quantity)), MovementTypes.Adjustment,
                request.Reason, User.UserName(), null, ct);
            return NoContent();
        }

        /// <summary>Stock count result or initial load: sets on-hand (and optionally min/max) per SKU.</summary>
        [HttpPost("stock/{locationCode}/counts")]
        [Authorize(Policy = Policies.Adjust)]
        public async Task<IActionResult> Count(string locationCode, [FromBody] List<StockCountLine> lines, [FromQuery] string reference, CancellationToken ct)
        {
            var count = await _stock.SetCountsAsync(locationCode, lines, reference ?? "count " + DateTime.UtcNow.ToString("yyyy-MM-dd"), User.UserName(), ct);
            return Ok(new { lines = count });
        }

        [HttpGet("movements")]
        public async Task<IActionResult> Movements([FromQuery] string location, [FromQuery] string sku, [FromQuery] int take = 200)
        {
            var q = _db.Movements.AsNoTracking();
            if (!string.IsNullOrEmpty(location)) q = q.Where(m => m.LocationCode == location);
            if (!string.IsNullOrEmpty(sku)) q = q.Where(m => m.Sku == sku.ToUpper());
            return Ok(await q.OrderByDescending(m => m.Id).Take(Math.Clamp(take, 1, 2000)).ToListAsync());
        }

        [HttpGet("reservations/{orderId:guid}")]
        public async Task<IActionResult> Reservations(Guid orderId) =>
            Ok(await _db.Reservations.AsNoTracking().Where(r => r.OrderId == orderId).OrderBy(r => r.CreatedUtc).ToListAsync());
    }

    public class AvailabilityQuery
    {
        public List<string> Skus { get; set; } = new List<string>();
        public List<string> Locations { get; set; }
        public string Country { get; set; }
    }

    public class AdjustmentRequest
    {
        public string Reason { get; set; }
        public List<AdjustmentLine> Lines { get; set; } = new List<AdjustmentLine>();
    }

    public class AdjustmentLine
    {
        public string Sku { get; set; }
        public int Quantity { get; set; }
    }
}

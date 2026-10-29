using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanArsdel.Allocation.Api.Data;
using VanArsdel.Allocation.Api.Services;
using VanArsdel.BuildingBlocks.Exceptions;
using VanArsdel.BuildingBlocks.Security;
using VanArsdel.Contracts.Orders;

namespace VanArsdel.Allocation.Api.Controllers
{
    [ApiController]
    [Route("api")]
    [Authorize(Policy = Policies.Read)]
    public class AllocationController : ControllerBase
    {
        private readonly AllocationDbContext _db;
        private readonly AllocationService _allocation;

        public AllocationController(AllocationDbContext db, AllocationService allocation)
        {
            _db = db;
            _allocation = allocation;
        }

        [HttpGet("decisions/{orderId:guid}")]
        public async Task<IActionResult> Decisions(Guid orderId) =>
            Ok(await _db.Decisions.AsNoTracking().Where(d => d.OrderId == orderId).OrderBy(d => d.Id).ToListAsync());

        [HttpGet("decisions")]
        public async Task<IActionResult> Recent([FromQuery] bool failedOnly, [FromQuery] int take = 100)
        {
            var q = _db.Decisions.AsNoTracking();
            if (failedOnly) q = q.Where(d => !d.Success);
            return Ok(await q.OrderByDescending(d => d.Id).Take(Math.Clamp(take, 1, 1000)).ToListAsync());
        }

        [HttpGet("rules")]
        public async Task<IActionResult> Rules() => Ok(await _db.Rules.AsNoTracking().OrderBy(r => r.Country).ToListAsync());

        [HttpPut("rules/{country}")]
        [Authorize(Policy = Policies.Admin)]
        public async Task<IActionResult> SaveRules(string country, [FromBody] AllocationRuleSet rules)
        {
            var existing = await _db.Rules.FindAsync(country.ToUpperInvariant()) ?? throw new NotFoundException("Rules", country);
            existing.MaxShipments = Math.Clamp(rules.MaxShipments, 1, 4);
            existing.AllowShipFromStore = rules.AllowShipFromStore;
            existing.AllowCrossBorderStores = rules.AllowCrossBorderStores;
            existing.StoreMinimumLeft = Math.Max(0, rules.StoreMinimumLeft);
            existing.ReservationHours = Math.Clamp(rules.ReservationHours, 1, 336);
            existing.UpdatedBy = User.UserName();
            existing.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(existing);
        }

        /// <summary>Dry run: shows where an order would ship from, without reserving.</summary>
        [HttpPost("simulate")]
        public async Task<IActionResult> Simulate([FromBody] AllocateOrder order, CancellationToken ct)
        {
            order.OrderId = Guid.NewGuid();
            order.OrderNo = "SIM-" + order.OrderId.ToString("N")[..8];
            var outcome = await _allocation.AllocateAsync(order, ct);
            if (outcome.Success) await _allocation.ReleaseAsync(order.OrderId, "simulation", ct);
            return Ok(outcome);
        }
    }
}

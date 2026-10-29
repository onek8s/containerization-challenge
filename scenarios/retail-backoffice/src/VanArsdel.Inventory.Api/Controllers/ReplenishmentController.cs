using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanArsdel.Inventory.Api.Data;
using VanArsdel.Inventory.Api.Services;

namespace VanArsdel.Inventory.Api.Controllers
{
    [ApiController]
    [Route("api/replenishment")]
    [Authorize(Policy = Policies.Read)]
    public class ReplenishmentController : ControllerBase
    {
        private readonly ReplenishmentService _replenishment;
        private readonly InventoryDbContext _db;

        public ReplenishmentController(ReplenishmentService replenishment, InventoryDbContext db)
        {
            _replenishment = replenishment;
            _db = db;
        }

        /// <summary>Runs replenishment for all stores or the given ones (scheduler, 04:30).</summary>
        [HttpPost("run")]
        [Authorize(Policy = Policies.Adjust)]
        public async Task<IActionResult> Run([FromBody] List<string> storeCodes, CancellationToken ct)
        {
            var proposals = await _replenishment.RunAsync(storeCodes ?? new List<string>(), ct);
            return Ok(new { proposals = proposals.Count, lines = proposals.Sum(p => p.Lines.Count), units = proposals.Sum(p => p.Lines.Sum(l => l.Quantity)) });
        }

        [HttpGet("proposals")]
        public async Task<IActionResult> Proposals([FromQuery] string store, [FromQuery] int take = 50)
        {
            var q = _db.ReplenishmentProposals.AsNoTracking().Include(p => p.Lines).AsQueryable();
            if (!string.IsNullOrEmpty(store)) q = q.Where(p => p.StoreCode == store);
            return Ok(await q.OrderByDescending(p => p.CreatedUtc).Take(take).ToListAsync());
        }
    }
}
